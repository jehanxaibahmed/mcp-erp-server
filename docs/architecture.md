# Architecture

The server follows a layered ("clean") architecture. Dependencies point inwards only, and
`tests/ErpMcp.UnitTests/Architecture/LayeringTests.cs` fails the build if that rule is broken.

```
┌──────────────────────────────────────────────────────────────┐
│ ErpMcp.Server          MCP host: stdio + HTTP/OAuth, tool    │
│                        adapters, filters, operator CLI       │
├──────────────────────────────────────────────────────────────┤
│ ErpMcp.Infrastructure  PostgreSQL (Npgsql + Dapper),         │
│                        migrations, audit log writer          │
├──────────────────────────────────────────────────────────────┤
│ ErpMcp.Application     Use cases, ports (interfaces),        │
│                        validation, permission scopes         │
├──────────────────────────────────────────────────────────────┤
│ ErpMcp.Domain          Entities, value objects, business     │
│                        rules (e.g. order approval states)    │
└──────────────────────────────────────────────────────────────┘
```

| Project | May reference | Must not reference |
| --- | --- | --- |
| `ErpMcp.Domain` | nothing | every other layer, any NuGet package |
| `ErpMcp.Application` | Domain | Npgsql, Dapper, MCP SDK |
| `ErpMcp.Infrastructure` | Application, Domain | MCP SDK |
| `ErpMcp.Server` | everything | — |

## Why this shape

- **MCP tools are thin adapters.** A tool method parses arguments, calls one application
  use case and shapes the result for the model. Business rules never live in a tool, so they
  can be unit tested without an MCP client.
- **The database is a detail.** The application layer defines repository ports; only the
  infrastructure layer knows about SQL. Integration tests run against a real PostgreSQL in a
  container.
- **Cross-cutting safety lives at the edge.** Permission scopes and audit logging are MCP
  request filters in the server project, so every tool gets them and no tool can forget them.

## Repository layout

```
├── src/
│   ├── ErpMcp.Domain/
│   ├── ErpMcp.Application/
│   ├── ErpMcp.Infrastructure/
│   └── ErpMcp.Server/
├── tests/
│   ├── ErpMcp.UnitTests/          fast, no I/O
│   └── ErpMcp.IntegrationTests/   real PostgreSQL via Testcontainers (needs Docker)
├── scripts/            helper scripts (MCP smoke client)
├── docs/               architecture, security, audit, database, tools, client guides
├── docker-compose.yml  local PostgreSQL (+ the server under the "tools" profile)
├── Dockerfile          multi-stage, runs as non-root
├── .mcp.json           project-scoped server for Claude Code
├── Directory.Build.props     shared compiler settings (warnings as errors)
└── Directory.Packages.props  central NuGet versions
```

## Build and test

```bash
dotnet build ErpMcp.slnx
dotnet test --solution ErpMcp.slnx
python3 scripts/mcp_smoke.py get_server_info   # talk to the server over stdio
```
