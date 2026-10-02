# Tool reference

Every tool publishes an `outputSchema` and returns its result twice: as `structuredContent`
(typed, matching the schema) and as JSON text, for clients that don't read structured output yet. Enum values use snake_case (`pending_approval`) in both
arguments and results. Codes are case-insensitive on input and normalised to upper case.

When a call fails for a reason the agent can fix, such as a malformed code, an unknown record or
an out-of-range page size, the result has `isError: true` and a one-line message, for example
`Invalid 'sku': expected a code like 'BEV-0001'.` Unexpected failures return a generic error
and never include SQL or stack traces.

## Read tools

All read tools are annotated `readOnlyHint: true`, `idempotentHint: true`, `openWorldHint: false`.

| Tool | Arguments | Returns |
| --- | --- | --- |
| `search_customers` | `query?`, `status?` (`active`, `on_hold`, `closed`), `limit?`, `offset?` | Page of customers |
| `get_customer` | `customerCode` | Customer + order stats (total, open count/value, last order) |
| `search_products` | `query?`, `category?`, `includeInactive?`, `limit?`, `offset?` | Page of products |
| `get_product` | `sku` | Product |
| `list_product_categories` | — | `{ categories: [{ name, activeProductCount }] }` |
| `get_stock_level` | `sku` | Per-warehouse on-hand, reserved, available and reorder level |
| `list_low_stock` | `warehouseCode?`, `limit?`, `offset?` | Page of stock rows below reorder level, most urgent first |
| `list_orders` | `customerCode?`, `status?`, `createdFrom?`, `createdTo?` (YYYY-MM-DD, inclusive), `limit?`, `offset?` | Page of order summaries, newest first |
| `get_order` | `orderNumber` | Order with lines, approval decision, review flags, warehouse allocations and fulfilment/cancellation details |
| `get_server_info` | — | Server name and version |

### Paging

List tools return `{ items, totalCount, limit, offset, hasMore }`. `limit` defaults to 20 and is
capped at 100.

### Example

```jsonc
// tools/call get_stock_level { "sku": "dai-0002" }
{
  "sku": "DAI-0002",
  "productName": "Mature Cheddar Block 5kg",
  "isActive": true,
  "totalOnHand": 321,
  "totalAvailable": 288,
  "warehouses": [
    { "warehouseCode": "WH-BHM", "warehouseName": "Birmingham Depot", "quantityOnHand": 107,
      "quantityReserved": 11, "quantityAvailable": 96, "reorderLevel": 30, "isBelowReorderLevel": false }
    // ...
  ]
}
```

## Write tool

| Tool | Arguments | Returns |
| --- | --- | --- |
| `create_draft_order` | `customerCode`, `lines[]` (`{ sku, quantity }`), `notes?`, `idempotencyKey?` | `{ message, alreadyExisted, order }` |

Annotated `readOnlyHint: false`, `destructiveHint: false`, `idempotentHint: false`.

This is the **only** tool that writes, and it can only create orders in `pending_approval`.
Agents cannot approve, reject, fulfil or cancel orders, and cannot set prices. See
[approval-workflow.md](approval-workflow.md).

- **Hard rules** fail the call with `isError: true`: an inactive or closed customer, an unknown
  or discontinued SKU, a duplicate SKU, a quantity outside 1–10,000, more than 50 lines, or notes
  longer than 500 characters.
- **Soft concerns** create the draft but attach `reviewFlags` for the approver, such as
  `Insufficient stock for BEV-0001: requested 400, available 372.` or
  `Exceeds credit limit: open orders £0.00 + this order £5,916.00 > limit £5,000.00.`
- **Retries**: pass an `idempotencyKey` (8–100 chars). Repeating the call with the same key and
  the same lines returns the original draft with `alreadyExisted: true`. Reusing a key for
  different lines is an error.

## Prompts

Prompts are reusable task templates that users pick in their client (for example via `/` in
Claude). They're scope-filtered like tools, and their arguments are validated before being
embedded in the prompt text.

| Prompt | Arguments | Scope | What it does |
| --- | --- | --- | --- |
| `review_low_stock` | `warehouseCode?` | `inventory:read` | Low-stock table with "transfer or reorder" suggestions. Never creates orders |
| `customer_account_review` | `customerCode` | `customers:read` | Credit headroom, open orders, recent activity, items to act on |
| `draft_order_from_request` | `customerCode`, `request` | `orders:draft` | Resolves free text to SKUs, confirms with the user, then drafts with an idempotency key |

`draft_order_from_request` puts the customer's words between `<<<REQUEST … REQUEST>>>` markers
and tells the model to treat them as data. An invalid argument returns a JSON-RPC `InvalidParams` error.

## Resources

| URI | Contents |
| --- | --- |
| `erp://guide` | Markdown orientation: code formats, stock terms, order lifecycle, and the tools this server instance allows |

ERP **data** is deliberately not exposed as resources. Resource reads bypass the per-tool scope
checks and the audit trail, so data stays behind tools.
