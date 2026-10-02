# Session notes: MCP ERP Server

_Last updated: 2026-10-02_

## Status: complete (100%), waiting for your review

Everything is built, tested and open as **12 stacked PRs**. CI is green on all of them, and none
are merged. Review and merge them **in order, 1 → 12**. Each PR's base is the one before it.

| PR | What it adds |
| --- | --- |
| [#1](https://github.com/jehanxaibahmed/mcp-erp-server/pull/1) | Layered .NET 10 solution, MCP stdio host, architecture tests, CI |
| [#2](https://github.com/jehanxaibahmed/mcp-erp-server/pull/2) | Postgres schema, synthetic seed data, DbUp migrations, Compose, Testcontainers |
| [#3](https://github.com/jehanxaibahmed/mcp-erp-server/pull/3) | 10 read-only tools (customers, products, stock, orders) |
| [#4](https://github.com/jehanxaibahmed/mcp-erp-server/pull/4) | `create_draft_order` + human approval CLI |
| [#5](https://github.com/jehanxaibahmed/mcp-erp-server/pull/5) | Permission scopes (read-only default), unknown-argument rejection |
| [#6](https://github.com/jehanxaibahmed/mcp-erp-server/pull/6) | Append-only audit log + `erp-mcp audit` |
| [#7](https://github.com/jehanxaibahmed/mcp-erp-server/pull/7) | Client setup guide, Dockerfile, `.mcp.json`, README |
| [#8](https://github.com/jehanxaibahmed/mcp-erp-server/pull/8) | Stock reservation on approval; `orders fulfil` / `orders cancel` |
| [#9](https://github.com/jehanxaibahmed/mcp-erp-server/pull/9) | Typed tool output (`outputSchema`), 3 MCP prompts, `erp://guide` resource |
| [#10](https://github.com/jehanxaibahmed/mcp-erp-server/pull/10) | Least-privilege DB roles, fail-closed audit mode, audit log shipping |
| [#11](https://github.com/jehanxaibahmed/mcp-erp-server/pull/11) | Streamable HTTP transport, OAuth bearer auth, per-user scopes, "on behalf of" |
| [#12](https://github.com/jehanxaibahmed/mcp-erp-server/pull/12) | `scripts/demo.sh` end-to-end demo, `docs/walkthrough.md`, README refresh |

**Tests:** 228/228 pass (unit, Postgres integration, and end-to-end over both stdio and HTTP).
The Release build has 0 warnings.

## Next session

1. Merge #1 → #12 in order. If you delete each branch after merging, GitHub retargets the next PR
   to `main` automatically. If you squash-merge, rebase the next branch onto `main` before merging it.
2. After merging, run `./scripts/demo.sh` on `main` as a final check.
3. Optional:
   - Record a short Claude Desktop screen capture for the README. It couldn't be done from here, so `docs/walkthrough.md` stands in for it.
   - Point `Auth:Authority` at a real identity provider (Entra ID, Auth0, Keycloak) and try the
     HTTP transport with a real OAuth login.
   - Add CI coverage reporting.

## Notes and gotchas

- **.NET 10**, not the .NET 8 the original README planned. Only the 9 and 10 SDKs are installed here, and 10 is the LTS.
- **stdout is reserved for MCP** in stdio mode. All logs go to stderr.
- **Tests run on Microsoft.Testing.Platform**: `dotnet test --solution ErpMcp.slnx`. Docker is required.
- **Local dev database has extra rows** from manual testing and demo runs. To reset:
  `docker compose down -v && docker compose up -d --wait && dotnet run --project src/ErpMcp.Server -- migrate --seed`
- **HTTP mode** needs `Auth:Audience`, plus exactly one of `Auth:Authority` or `Auth:DevSigningKey` (local only).
  `scripts/dev_token.py` mints dev tokens matching the Compose `http` profile.
- When you add a tool or prompt, give it `[RequiresScope]`. Start-up and the tests fail without it.
  Also update the exact tool lists asserted in the E2E tests.
