# Security model

An MCP server hands a language model the ability to act. The model may be wrong, and it may
have read a prompt injection, so the server never relies on the model to behave. Each control
below is enforced by code that runs on every call.

## 1. Permission scopes

Every tool declares one scope with `[RequiresScope]`. If a tool is missing the attribute, start-up
and a unit test both fail.

| Scope | Tools |
| --- | --- |
| `customers:read` | `search_customers`, `get_customer` |
| `catalog:read` | `search_products`, `get_product`, `list_product_categories` |
| `inventory:read` | `get_stock_level`, `list_low_stock` |
| `orders:read` | `list_orders`, `get_order` |
| `orders:draft` | `create_draft_order` |
| *(none)* | `get_server_info` |

A server instance is granted scopes through configuration. Over the HTTP transport, the caller's
OAuth token narrows that grant per request. See [http-transport.md](http-transport.md). **The default is `read`**, which
expands to every `*:read` scope. Writing must be opted into explicitly:

```bash
Security__Scopes="read orders:draft"        # environment variable
--Security:Scopes="customers:read orders:read"   # command-line override
```

Scopes are enforced twice:

1. **`tools/list` filter**: ungranted tools are hidden, so the model never considers them.
2. **`tools/call` filter**: ungranted tools are refused even when called by name. The error says
   which scope is missing.

An unknown scope in configuration (`orders:write`) stops start-up with exit code 1. A typo
can't silently grant more or less than intended. Agents can call `get_server_info` to see
what they have been granted.

## 2. Input validation

| Layer | What it checks |
| --- | --- |
| MCP SDK | JSON types against each tool's input schema |
| `ArgumentValidationFilter` | Rejects **unknown argument names** with a "did you mean" hint, and caps total argument size (`Security:MaxArgumentBytes`, default 32 KB) |
| `Guard` (application) | Code formats (`CUST-0001`, `BEV-0001`, `SO-100001`, `WH-MAN`), enum values, paging limits (1–100), search length (≤ 100), idempotency keys, actor ids |
| `DraftOrder` (domain) | Business rules: active customer and products, quantities, line count, duplicate SKUs |
| PostgreSQL | `CHECK` constraints mirror the key invariants as a last line of defence |

Unknown arguments are rejected because the SDK would otherwise ignore them silently. An agent
that sent `customer_code` instead of `customerCode` would get every customer's orders back and
probably not notice.

All SQL is parameterised. Free-text search escapes `LIKE` wildcards, so user text is always
matched literally.

## 3. Writes need a human

`orders:draft` only ever creates `pending_approval` orders. Approving is not an MCP tool. See
[approval-workflow.md](approval-workflow.md).

## 4. Errors don't leak

Expected failures (validation, not found, rule violations, permission denied) return
`isError: true` with an actionable one-line message. Anything unexpected returns the SDK's
generic error, and details go only to the server's stderr log.

## 5. Everything is audited

Every call, including refused ones, is written to an append-only table. You can choose
fail-closed mode (no audit, no action) and log shipping. See [audit.md](audit.md).

## 6. Least-privilege database access

The server can run as the `erp_app` role. It can do its job, but it can't alter the schema,
delete ERP records, or rewrite audit history, even if the process is compromised. Migrations
use a separate owner connection. See [audit.md](audit.md#least-privilege-database-roles).

## Filter pipeline

```
tools/call ─▶ ToolErrorFilter ─▶ ToolCallAuditor ─▶ scope check ─▶ argument validation ─▶ tool ─▶ application ─▶ SQL
               (maps errors)      (records outcome)   (403-style)     (names, size)
```
