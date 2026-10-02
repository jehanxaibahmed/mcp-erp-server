# Walkthrough

A real run of [`scripts/demo.sh`](../scripts/demo.sh), which you can reproduce with `./scripts/demo.sh`.
It plays both parts. An **agent** calls MCP tools through the bundled stdio client, and a
**person** uses the operator CLI. Output is unedited apart from trimming long JSON.

## Agent (read-only): is BEV-0005 in stock?

```
{
  "sku": "BEV-0005",
  "productName": "Hot Chocolate Powder 2kg",
  "isActive": true,
  "totalOnHand": 381,
  "totalAvailable": 375,
  "warehouses": [
    {
      "sku": "BEV-0005",
      "productName": "Hot Chocolate Powder 2kg",
      "warehouseCode": "WH-BHM",
      "warehouseName": "Birmingham Depot",
```

## Agent (read-only) tries to draft. The tool isn't even offered, and a call by name is refused

```
{
  "content": [
    {
      "type": "text",
      "text": "'create_draft_order' requires the 'orders:draft' scope, which this server has not been granted. Ask an administrator to enable it."
    }
  ],
  "isError": true
}
```

## Agent (read orders:draft) drafts an order for Riverside Care Home

```
Draft SO-100066 created and is pending human approval. It will not be fulfilled until approved.
review flags: none
```

## Agent retries with the same idempotency key (e.g. after a timeout)

```
Draft SO-100066 already existed for this idempotency key. No new order was created.
```

## Operator reviews the queue

```
ORDER       CREATED (UTC)     SOURCE  CUSTOMER                         LINES        TOTAL
SO-100066   2026-10-02 19:00  agent   CUST-0004 Riverside Care Home        2      £151.20
SO-100060   2026-10-02 05:53  agent   CUST-0015 Pennine Events Cateri…     1      £134.40
SO-100059   2026-09-30 10:53  manual  CUST-0007 Moorland Golf Club         4      £393.45
SO-100058   2026-09-28 15:53  manual  CUST-0019 Kingsway Primary Acad…     3      £230.40
SO-100057   2026-09-25 20:53  manual  CUST-0010 St. Anne's Community …     2      £156.40
5 of 5 pending.
```

## An agent identity tries to approve: refused

```
error: This action must be performed by a person, not an agent identity.
```

## Operator approves; stock is reserved

```
Approved SO-100066 (£151.20) for Riverside Care Home. Stock reserved:
  line  1  BEV-0005       6 from WH-MAN
  line  2  DAI-0005       2 from WH-BHM
```

## Warehouse fulfils; reserved stock ships

```
Fulfilled SO-100066: 8 units shipped from WH-MAN, WH-BHM.
SO-100066  [fulfilled]  CUST-0004 Riverside Care Home
Created 2026-10-02 19:00 UTC by agent:mcp-smoke
Decided 2026-10-02 19:00 UTC by ops.lead@example.com
Closed 2026-10-02 19:00 UTC by warehouse@example.com
Notes: Deliver to kitchen

   1. BEV-0005  Hot Chocolate Powder 2kg                  6 x    £12.90 =     £77.40
   2. DAI-0005  Free Range Eggs x180                      2 x    £36.90 =     £73.80
  Total: £151.20

Shipped from:
  line  1  BEV-0005       6 from WH-MAN
  line  2  DAI-0005       2 from WH-BHM
```

## Audit trail for this order

```
TIME (UTC)          CH  ACTOR                      ACTION                   OUTCOME      MS  DETAIL
2026-10-02 19:00:52 cli warehouse@example.com      orders.fulfil            success     189  SO-100066
2026-10-02 19:00:52 cli ops.lead@example.com       orders.approve           success     200  SO-100066
2026-10-02 19:00:51 cli agent:helper               orders.approve           rejected      9  This action must be performed by a person, not an agent identity.
2026-10-02 19:00:50 mcp agent:mcp-smoke            create_draft_order       success     100  SO-100066
2026-10-02 19:00:49 mcp agent:mcp-smoke            create_draft_order       success     148  SO-100066
2026-10-02 19:00:48 mcp agent:mcp-smoke            create_draft_order       denied        1  'create_draft_order' requires the 'orders:draft' scope, which this se…
```

## What this shows

| Guardrail | Seen above |
| --- | --- |
| Read-only by default | The read-only agent gets a clear `orders:draft` refusal, logged as `denied` |
| Prices from the catalogue | The agent sent SKUs and quantities only, and line prices came from the ERP |
| Safe retries | The same idempotency key returned the same draft, so no duplicate was created |
| Human approval | `agent:helper` couldn't approve. A person had to |
| Stock integrity | Approval reserved stock per warehouse, and fulfilment shipped exactly those units |
| Full audit | Every MCP call and CLI decision, refused ones included, is in the trail |
