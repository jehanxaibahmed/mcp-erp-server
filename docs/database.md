# Database

PostgreSQL holds a small, synthetic **wholesale ERP**: a food-service distributor with
customers, a product catalogue, stock across three warehouses, and sales orders.

```
customers 1──* orders 1──* order_lines *──1 products 1──* stock_levels *──1 warehouses
```

| Table | Purpose |
| --- | --- |
| `erp.customers` | Trade accounts with a credit limit and status (`active`, `on_hold`, `closed`) |
| `erp.products` | Catalogue keyed by SKU (`BEV-0001`), with price, unit of measure and active flag |
| `erp.warehouses` | Three depots (`WH-MAN`, `WH-BHM`, `WH-LDS`) |
| `erp.stock_levels` | On-hand, reserved and reorder level per product and warehouse |
| `erp.orders` | Sales orders. Agent-drafted orders start as `pending_approval` |
| `erp.order_lines` | Order lines. `line_total` is a generated column |

Business invariants are enforced in the schema as well as in code. For example, a pending order
cannot carry a decision timestamp, a rejected order must have a reason, and reserved stock can
never exceed on-hand stock.

## Migrations

SQL scripts are embedded in `ErpMcp.Infrastructure` and applied with DbUp:

- `Persistence/Scripts/Migrations/` holds schema changes, journaled in `public.schema_versions`.
- `Persistence/Scripts/Seed/` holds synthetic sample data, journaled separately in
  `public.seed_versions`, so a real database can take migrations without ever receiving demo data.

Scripts are immutable once merged. To change the schema, add a new numbered script.

## Local setup

```bash
docker compose up -d --wait                                  # PostgreSQL on localhost:5433
dotnet run --project src/ErpMcp.Server -- migrate --seed     # schema + sample data
```

The server also migrates on startup when `Database:MigrateOnStartup` is `true`, which is the
default in `appsettings.json`. Override settings with environment variables, for example
`Database__ConnectionString=...`.

All sample data is fictional. Emails use the reserved `.example` domain.
