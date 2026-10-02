# Session notes: MCP ERP Server

_Last updated: 2026-10-02_

## Status

All six original roadmap items are built, tested and open as **7 stacked PRs**. Nothing is merged
yet. Review and merge them **in order, 1 → 7**.

| PR | Branch | Base | What it adds | CI |
| --- | --- | --- | --- | --- |
| [#1](https://github.com/jehanxaibahmed/mcp-erp-server/pull/1) | `feat/01-solution-scaffold` | `main` | Layered .NET 10 solution, MCP stdio host, architecture tests, CI | ✅ |
| [#2](https://github.com/jehanxaibahmed/mcp-erp-server/pull/2) | `feat/02-erp-schema-seed` | #1 | Postgres schema, synthetic seed data, DbUp migrations, Compose, Testcontainers | ✅ |
| [#3](https://github.com/jehanxaibahmed/mcp-erp-server/pull/3) | `feat/03-read-tools` | #2 | 10 read-only tools (customers, products, stock, orders) | ✅ |
| [#4](https://github.com/jehanxaibahmed/mcp-erp-server/pull/4) | `feat/04-draft-orders-approval` | #3 | `create_draft_order` tool + human approval CLI | ✅ |
| [#5](https://github.com/jehanxaibahmed/mcp-erp-server/pull/5) | `feat/05-validation-and-scopes` | #4 | Permission scopes (read-only default), unknown-argument rejection | ✅ |
| [#6](https://github.com/jehanxaibahmed/mcp-erp-server/pull/6) | `feat/06-audit-log` | #5 | Append-only audit log + `erp-mcp audit` viewer | ✅ |
| [#7](https://github.com/jehanxaibahmed/mcp-erp-server/pull/7) | `feat/07-client-guide-docker` | #6 | Client setup guide, Dockerfile, `.mcp.json`, README rewrite | ⏳ was still running at hand-off |

**Tests:** 158/158 pass locally (unit, Postgres integration, and end-to-end over stdio).
The Release build has 0 warnings.

## What's done

- **Architecture:** `Domain` ← `Application` ← `Infrastructure` ← `Server`, enforced by tests. See `docs/architecture.md`.
- **Database:** wholesale ERP schema (customers, products, warehouses, stock, orders, lines) with
  invariants as `CHECK` constraints. Seed data is journaled separately from migrations. Migrations: `0001` schema, `0002` draft review, `0003` audit.
- **Read tools:** search and get for customers, products, stock and orders. Inputs are validated and
  normalised, and failures come back as actionable `isError` messages.
- **Draft orders:** prices come from the catalogue. Hard rules reject the draft. Soft issues (credit
  limit, stock) become `reviewFlags`. Drafts are idempotent, including under concurrent retries.
- **Approval:** operator CLI only (`erp-mcp orders pending|show|approve|reject`). Agent identities
  and order creators can't decide. When two approvers race, exactly one wins.
- **Scopes:** `customers:read`, `catalog:read`, `inventory:read`, `orders:read`, `orders:draft`.
  The default is `read`. Ungranted tools are hidden from listing and refused when called, and an
  unknown scope stops start-up.
- **Audit:** every call is logged, refused ones included, with outcome, actor, arguments (with
  redaction) and entity ref. Updates, deletes and truncates are blocked by a trigger.
- **Ops:** Dockerfile (runs as non-root), Compose `tools` profile, `.mcp.json`, and
  `scripts/mcp_smoke.py` (accepts `MCP_SERVER_CMD`).
- **Docs:** `docs/` covers architecture, database, tools, approval-workflow, security, audit and connecting-clients.

## Next session

### First: merge the stack
1. Confirm CI on #7 went green (the `docker` job is new).
2. Merge #1 into `main`. GitHub retargets #2 to `main` automatically if the merged branch is
   deleted. Repeat through #7. Rebase or squash merges are fine. If you squash, rebase the next
   branch onto `main` before merging it, or GitHub may show duplicate commits.

### Then: possible follow-ups (none started)
- [ ] **Streamable HTTP transport** (`ModelContextProtocol.AspNetCore`) with OAuth, so scopes come
  from the user's token instead of process config. This is the biggest remaining item, and the
  `GrantedScopes` and `AgentActor` seams are in place for it.
- [ ] **Stock reservation on approval:** pick a warehouse, increment `quantity_reserved`, and add a
  `fulfil` / `cancel` CLI for the remaining lifecycle transitions (`OrderStatusRules` already allows them).
- [ ] **Audit hardening:** give the app role INSERT-only grants on `audit.events`, add an optional
  fail-closed mode, and ship events off-box.
- [ ] **MCP resources/prompts:** e.g. a resource for the product catalogue, and a prompt for the
  "reorder low stock" workflow.
- [ ] **Tool `outputSchema`** (`UseStructuredContent = true`), so clients get typed results.
- [ ] Optionally add a demo GIF or screenshot of Claude Desktop using the tools to the README.

## Notes and gotchas

- **.NET 10, not .NET 8.** Only the 9 and 10 SDKs are installed here, and 10 is the current LTS.
  The README now says .NET 10.
- **stdout is reserved for MCP.** Logs go to stderr, and DbUp's console logger is silenced. Keep it that way.
- **Tests run on Microsoft.Testing.Platform** (set in `global.json`): use `dotnet test --solution ErpMcp.slnx`.
- **Local dev DB has extra rows** from manual testing (SO-100061 and SO-100062 are approved, plus
  audit events). To reset: `docker compose down -v && docker compose up -d --wait && dotnet run --project src/ErpMcp.Server -- migrate --seed`.
- The E2E test listing the read-only tools asserts the exact tool list. Update it when adding tools.
