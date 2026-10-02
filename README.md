# 🔌 MCP ERP Server

![Status](https://img.shields.io/badge/status-working%20sample-brightgreen?style=for-the-badge) ![.NET](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white) ![MCP](https://img.shields.io/badge/MCP-000000?style=for-the-badge&logo=anthropic&logoColor=white) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-336791?style=for-the-badge&logo=postgresql&logoColor=white)

> A Model Context Protocol (MCP) server that lets AI agents safely query and act on ERP data: orders, stock and customers.

## 🎯 Why this project

AI agents are only useful when they can reach real business systems, and only safe when those
systems don't trust them blindly. This server exposes a sample wholesale ERP as well-defined MCP
tools, so an agent can answer *"what's the stock level of this product?"* or draft an order. The
guardrails are enforced in code rather than left to the model:

- 🔒 **Read-only by default.** Writing needs an explicit `orders:draft` scope.
- 🧑‍⚖️ **Humans approve.** Agents can only *draft* orders. Approval is a separate operator CLI, deliberately not a tool.
- ✅ **Strict input validation.** Code formats, enums, paging, and even misspelled argument names are rejected with actionable errors.
- 📜 **Append-only audit trail** of every tool call, including refused ones.

## ✨ What an agent can do

| Tool | Scope |
| --- | --- |
| `search_customers`, `get_customer` | `customers:read` |
| `search_products`, `get_product`, `list_product_categories` | `catalog:read` |
| `get_stock_level`, `list_low_stock` | `inventory:read` |
| `list_orders`, `get_order` | `orders:read` |
| `create_draft_order` *(pending human approval)* | `orders:draft` |
| `get_server_info` | — |

Full reference: [docs/tools.md](docs/tools.md)

## 🚀 Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```bash
docker compose up -d --wait                                   # PostgreSQL with a persistent volume
dotnet run --project src/ErpMcp.Server -- migrate --seed      # schema + synthetic sample data
python3 scripts/mcp_smoke.py get_stock_level '{"sku":"BEV-0001"}'
```

Then connect a real client (Claude Desktop, Claude Code, VS Code, MCP Inspector):
**[docs/connecting-clients.md](docs/connecting-clients.md)**. This repo includes a project
`.mcp.json`, so Claude Code picks the server up automatically.

### The approval loop

```bash
# Agent (with Security__Scopes="read orders:draft") calls create_draft_order → SO-100061 pending
dotnet run --project src/ErpMcp.Server -- orders pending
dotnet run --project src/ErpMcp.Server -- orders show SO-100061       # lines + review flags
dotnet run --project src/ErpMcp.Server -- orders approve SO-100061 --by you@example.com   # reserves stock
dotnet run --project src/ErpMcp.Server -- orders fulfil  SO-100061 --by warehouse@example.com
dotnet run --project src/ErpMcp.Server -- audit                       # who did what
```

## 🧱 Architecture

```
src/
├── ErpMcp.Domain           Entities and business rules (draft validation, order lifecycle). No dependencies.
├── ErpMcp.Application      Use cases, input guards, scopes, ports (repository/audit interfaces)
├── ErpMcp.Infrastructure   PostgreSQL via Npgsql + Dapper, DbUp migrations, audit writer
└── ErpMcp.Server           MCP host (stdio), thin tool adapters, filters, operator CLI
tests/
├── ErpMcp.UnitTests          Domain rules, guards, scopes, CLI parsing, layering rules
└── ErpMcp.IntegrationTests   Real Postgres (Testcontainers) + end-to-end over stdio with the MCP client
```

Every tool call goes through this pipeline:

```
tools/call ─▶ error mapping ─▶ audit ─▶ scope check ─▶ argument validation ─▶ tool ─▶ use case ─▶ SQL
```

Read more: [architecture](docs/architecture.md) · [security model](docs/security.md) ·
[approval workflow](docs/approval-workflow.md) · [audit trail](docs/audit.md) · [database](docs/database.md)

## 🧪 Tests

```bash
dotnet test --solution ErpMcp.slnx     # needs Docker for the integration tests
```

The suite has 158 tests: unit, repository integration against PostgreSQL, and end-to-end tests
that launch the compiled server and drive it with the official MCP C# client. Concurrency is
covered too, including idempotent draft retries racing and two approvers deciding at once.

## 🧰 Stack

- .NET 10 / C#, official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) 2.2
- PostgreSQL 17, Npgsql, Dapper, DbUp
- xUnit v3, Shouldly, Testcontainers
- Docker / Compose, GitHub Actions

## 🗺️ Roadmap

- [x] Sample ERP schema and seed data
- [x] Read tools: customers, products, stock, orders
- [x] Draft-order tool that requires human approval
- [x] Input validation and permission scopes
- [x] Audit log of every tool call
- [x] Guide for connecting to MCP clients
- [ ] Streamable HTTP transport with OAuth-backed per-user scopes
- [x] Stock reservation on approval, fulfilment and cancellation

## 📌 Notes

Uses **synthetic sample data only**. All companies, emails (`.example`) and phone numbers are fictional.

---

Built by [Jahanzaib Ahmad](https://github.com/jehanxaibahmed) · Full Stack Engineer · AI & LLM Systems
