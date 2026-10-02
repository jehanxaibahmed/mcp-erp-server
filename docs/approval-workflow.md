# Human approval workflow

Agents can **propose** orders. Only people can **decide** on them.

```
 AI agent ──create_draft_order──▶ [pending_approval] ──approve──▶ approved ──fulfil──▶ fulfilled
                                         │   (stock reserved)      │  (stock shipped)
                                         ├──reject──▶ rejected     └──cancel──▶ cancelled (stock released)
                                         └──cancel──▶ cancelled
                                   review flags shown to the operator
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
| Two operators deciding at once: exactly one wins | Order row locked `FOR UPDATE` and status re-checked |
| Approval reserves stock; concurrent approvals can't promise the same units twice | `StockAllocator`, stock rows locked `FOR UPDATE` |
| Fulfil and cancel are person-only too, and cancelling needs a reason | `OrderFulfilmentService` |

## Operator commands

```bash
erp-mcp orders pending
erp-mcp orders show SO-100061
erp-mcp orders approve SO-100061 --by ops.lead@example.com
erp-mcp orders reject  SO-100061 --by ops.lead@example.com --reason "Over credit limit"
erp-mcp orders fulfil  SO-100061 --by warehouse@example.com
erp-mcp orders cancel  SO-100061 --by ops.lead@example.com --reason "Customer cancelled"
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

## Stock reservation

Approval reserves stock in the same transaction that changes the order's status:

1. Lock the order row and confirm it is still `pending_approval`.
2. Lock every `stock_levels` row for the order's products, in key order so concurrent
   approvals queue instead of deadlocking.
3. `StockAllocator` (domain) chooses warehouses. It uses one warehouse if one can cover the
   whole line, picking the best-stocked to keep the others balanced. Otherwise it splits the
   line, drawing from the best-stocked warehouses first.
4. If total stock is short, approval is refused with the exact shortfall and nothing changes:
   `Cannot approve SO-100063: insufficient stock (MEA-0001 needs 900, available 712).`
5. Increase `quantity_reserved`, record rows in `erp.order_allocations` and mark the order approved.

```
Approved SO-100063 (£8,531.00) for Westside Hospital Trust. Stock reserved:
  line  1  MEA-0001      40 from WH-BHM
  line  1  MEA-0001     210 from WH-LDS
  line  2  BEV-0003       5 from WH-BHM
```

**Fulfil** decreases both on-hand and reserved stock by the allocated quantities. **Cancel** of
an approved order decreases reserved stock only, which puts it back on the shelf. Agents see the
effect through `get_stock_level`, since available stock drops as soon as an order is approved,
and through the `allocations` on `get_order`.

Orders approved before migration `0004` (the seed data) have no allocations. Fulfilling them
only changes their status.
