# Audit trail

Every MCP tool call is written to `audit.events`, whether it succeeded, was refused or failed.
So is every operator decision made through the CLI.

| Column | Example |
| --- | --- |
| `occurred_at` | `2026-10-02 18:14:59+00` |
| `channel` | `mcp` (tool call) or `cli` (operator command) |
| `actor` | `agent:claude-desktop`, `ops.lead@example.com` |
| `session_id` | Groups one MCP connection (one stdio process) or one CLI run |
| `client_name`, `client_version` | As reported by the MCP client during `initialize` |
| `action` | `get_stock_level`, `create_draft_order`, `orders.approve` |
| `arguments` | `jsonb`, after redaction |
| `outcome` | `success`, `invalid`, `not_found`, `rejected`, `denied`, `failed` |
| `error_message` | The message returned to the caller |
| `duration_ms` | Handling time |
| `entity_ref` | Record created or changed, e.g. `SO-100062` |

## Viewing it

```bash
erp-mcp audit                                    # latest 50 events
erp-mcp audit --outcome denied                   # refused for missing scope
erp-mcp audit --action create_draft_order --limit 20
erp-mcp audit --actor ops.lead@example.com
```

```
TIME (UTC)          CH  ACTOR                      ACTION                   OUTCOME      MS  DETAIL
2026-10-02 18:15:02 cli ops.lead@example.com       orders.approve           success     175  SO-100062
2026-10-02 18:15:01 cli agent:x                    orders.approve           rejected      9  Orders must be approved or rejected by a person, not an agent identit…
2026-10-02 18:15:00 mcp agent:mcp-smoke            get_order                not_found    53  Order 'SO-424242' was not found.
2026-10-02 18:14:59 mcp agent:mcp-smoke            create_draft_order       success     138  SO-100062
2026-10-02 18:14:58 mcp agent:mcp-smoke            create_draft_order       denied        1  'create_draft_order' requires the 'orders:draft' scope, which this se…
2026-10-02 18:14:57 mcp agent:mcp-smoke            list_orders              invalid       3  Invalid 'customer_code': is not an argument of 'list_orders'. Did you…
2026-10-02 18:14:55 mcp agent:mcp-smoke            get_stock_level          success      97  {"sku": "BEV-0003"}
```

The trail is deliberately not exposed as an MCP tool. Agents don't need to read it, and if they
could, they could probe what other sessions have been doing.

## Guarantees and limits

- **Append-only, twice over.** A statement-level trigger rejects `UPDATE`, `DELETE` and
  `TRUNCATE` on `audit.events`. Separately, the `erp_app` role created by migration `0005` only
  has `INSERT`/`SELECT` on it, and as a non-owner it can't disable the trigger either. Run the
  server as an `erp_app` member (see below) and both protections apply.
- **Refused calls are recorded too.** The audit filter sits inside the error filter but outside
  the scope and argument checks, so denied and invalid calls appear with their reason.
- **Cancelled calls are recorded.** The audit write uses its own token.
- **Failure mode is configurable** (`Audit:FailureMode`):
  - `Open` (default): one event per call, written afterwards. If the write fails, the result is
    still returned and `AUDIT WRITE FAILED` is logged at Error level. This favours availability.
  - `Closed`: a `started` event is written **before** the tool runs. If that fails, the call is
    refused with *"The audit trail is unavailable… Nothing was executed."* The outcome event follows
    with the same `call_id`. This favours compliance, because nothing happens unrecorded.
- **Redaction.** Argument names listed in `Audit:RedactedArguments` are stored as `"[redacted]"`.
  Payloads over `Audit:MaxStoredArgumentBytes` are stored as `{"_truncated": true, "_bytes": n}`.

- **Log shipping.** With `Audit:EmitToLog: true`, every event is also written as a structured log
  line (event name `AuditEvent`) to stderr. Pair it with the JSON console formatter
  (`Logging__Console__FormatterName=json`) and any log collector gets an off-box copy.

```json
"Audit": {
  "FailureMode": "Closed",
  "EmitToLog": true,
  "RedactedArguments": ["notes"],
  "MaxStoredArgumentBytes": 8192
}
```

## Least-privilege database roles

Migration `0005` creates two `NOLOGIN` group roles (skipped with a notice if the migration user
can't create roles, as on some managed databases):

| Role | Can | Cannot |
| --- | --- | --- |
| `erp_app` | Read and write ERP tables; `INSERT`/`SELECT` audit events | `UPDATE`/`DELETE` audit, `DELETE` ERP rows, any DDL, disable triggers, read migration journals |
| `erp_auditor` | `SELECT` audit events | Write anything; read ERP data |

To run the server as `erp_app`, keep the owner only for migrations:

```sql
CREATE ROLE erp_mcp LOGIN PASSWORD 'change-me' IN ROLE erp_app;
```

```bash
Database__ConnectionString="Host=…;Username=erp_mcp;Password=change-me;Database=erp"
Database__MigrationConnectionString="Host=…;Username=erp;Password=…;Database=erp"
```

Integration tests log in as a real `erp_app` member, run the whole order workflow, and check
that every forbidden statement above fails with `42501 insufficient_privilege`.
