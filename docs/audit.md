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

- **Append-only.** A statement-level trigger rejects `UPDATE`, `DELETE` and `TRUNCATE` on
  `audit.events`. A database superuser can still disable the trigger. In production you would
  also give the application role `INSERT`-only grants and ship events to an external store.
- **Refused calls are recorded too.** The audit filter sits inside the error filter but outside
  the scope and argument checks, so denied and invalid calls appear with their reason.
- **Cancelled calls are recorded.** The audit write uses its own token.
- **Fail-open on audit write errors.** If the insert fails, the tool result is still returned and
  the server logs `AUDIT WRITE FAILED` at Error level on stderr. Failing reads because the audit
  store is down would make it a single point of failure. Change this in `ToolCallAuditor` if your
  compliance regime requires fail-closed.
- **Redaction.** Argument names listed in `Audit:RedactedArguments` are stored as `"[redacted]"`.
  Payloads over `Audit:MaxStoredArgumentBytes` are stored as `{"_truncated": true, "_bytes": n}`.

```json
"Audit": {
  "RedactedArguments": ["notes"],
  "MaxStoredArgumentBytes": 8192
}
```
