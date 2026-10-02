# Human approval workflow

Agents can **propose** orders. Only people can **decide** on them.

```
 AI agent ──create_draft_order──▶ [pending_approval] ──operator CLI──▶ approved ──▶ fulfilled
                                         │                    └──────▶ rejected (reason required)
                                         └── review flags shown to the operator
```

## Why approval is not an MCP tool

If approving were a tool, it would be one prompt injection away from an agent approving its own
order. Instead, decisions are made through the `erp-mcp orders` CLI. It runs as a separate process
that a person runs deliberately, with its own credentials, and it never exposes an MCP transport.

The application layer enforces this too, so a future HTTP or UI front end inherits the same rules:

| Rule | Where |
| --- | --- |
| Agents can only create `pending_approval` orders from source `agent` | `DraftOrderService`, `OrderWriter` |
| Prices come from the catalogue, never from the request | `DraftOrder.Create` |
| Only `pending_approval` → `approved` / `rejected` / `cancelled` is allowed | `OrderStatusRules` |
| An `agent:*` identity can never approve or reject | `OrderApprovalService` |
| Whoever created an order cannot also decide on it | `OrderApprovalService` |
| Rejections need a reason | `OrderApprovalService` and a DB `CHECK` constraint |
| Two operators deciding at once: exactly one wins | `UPDATE … WHERE status = 'pending_approval'` |

## Operator commands

```bash
erp-mcp orders pending
erp-mcp orders show SO-100061
erp-mcp orders approve SO-100061 --by ops.lead@example.com
erp-mcp orders reject  SO-100061 --by ops.lead@example.com --reason "Over credit limit"
```

(During development, run `dotnet run --project src/ErpMcp.Server -- orders pending` and so on.)

Example review:

```
SO-100061  [pending_approval]  CUST-0003 The Copper Kettle Cafe
Created 2026-10-02 18:05 UTC by agent:claude-desktop
Notes: Back door delivery

   1. BEV-0001  Ground Coffee Medium Roast 1kg          400 x    £14.50 =  £5,800.00
   2. DAI-0003  Salted Butter 250g x40                    2 x    £58.00 =    £116.00
  Total: £5,916.00

Review flags:
  ! Exceeds credit limit: open orders £0.00 + this order £5,916.00 > limit £5,000.00.
```

## Scope notes

Drafting and approval do not reserve stock. Reservation and fulfilment belong to the warehouse
system and are out of scope for this sample.
