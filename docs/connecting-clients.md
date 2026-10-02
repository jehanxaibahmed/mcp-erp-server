# Connecting MCP clients

The server speaks MCP over **stdio**. The client launches it as a child process and exchanges
JSON-RPC on stdin/stdout. Logs go to stderr, so they never corrupt the stream.

## 1. Prerequisites

```bash
docker compose up -d --wait                                   # PostgreSQL on localhost:5433
dotnet run --project src/ErpMcp.Server -- migrate --seed      # schema + sample data
dotnet publish src/ErpMcp.Server -c Release -o publish        # self-contained folder for clients
```

`publish/erp-mcp.dll` is what clients run. It reads `publish/appsettings.json`, so the defaults
point at the Compose database. Override anything with environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `Database__ConnectionString` | Compose database | Where the ERP lives |
| `Security__Scopes` | `read` | Granted scopes; add `orders:draft` to allow drafting ([security.md](security.md)) |
| `Database__MigrateOnStartup` | `true` | Apply pending migrations at start-up |
| `Audit__RedactedArguments__0` | — | Argument names to redact in the audit trail |

In the snippets below, replace `/ABS/PATH` with the absolute path to this repository.

## Claude Desktop

Edit `claude_desktop_config.json` (macOS: `~/Library/Application Support/Claude/`, Windows: `%APPDATA%\Claude\`):

```json
{
  "mcpServers": {
    "erp": {
      "command": "dotnet",
      "args": ["/ABS/PATH/publish/erp-mcp.dll"],
      "env": {
        "Security__Scopes": "read orders:draft"
      }
    }
  }
}
```

Restart Claude Desktop. The ERP tools appear under the tools menu.

## Claude Code

This repository ships a project-scoped [`.mcp.json`](../.mcp.json). Open Claude Code in the repo
root and approve the `erp` server when prompted. It runs read-only.

To add it to another project or globally:

```bash
claude mcp add erp -e Security__Scopes="read orders:draft" -- dotnet /ABS/PATH/publish/erp-mcp.dll
```

## VS Code (GitHub Copilot agent mode)

`.vscode/mcp.json`:

```json
{
  "servers": {
    "erp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["/ABS/PATH/publish/erp-mcp.dll"],
      "env": { "Security__Scopes": "read" }
    }
  }
}
```

## Docker instead of a local .NET install

```bash
docker compose build erp-mcp
```

Then use this as the server command in any client. It joins the Compose network and reaches
Postgres as `postgres`:

```json
{
  "command": "docker",
  "args": [
    "compose", "-f", "/ABS/PATH/docker-compose.yml", "--profile", "tools",
    "run", "--rm", "-T", "-e", "Security__Scopes=read", "erp-mcp"
  ]
}
```

## MCP Inspector (debugging)

```bash
npx @modelcontextprotocol/inspector dotnet publish/erp-mcp.dll
```

Or, without Node, use the bundled smoke client:

```bash
python3 scripts/mcp_smoke.py                                    # list tools
python3 scripts/mcp_smoke.py get_stock_level '{"sku":"BEV-0001"}'
```

## Things to ask the agent

- *"Which products are below their reorder level in Manchester?"*
- *"What's Riverside Care Home's credit limit, and how much do they have in open orders?"*
- *"Show me the last five orders for CUST-0011 and what was on the biggest one."*
- *"Draft an order for The Copper Kettle Cafe: 10 packs of medium roast coffee and 2 cases of butter."*
  This needs `orders:draft`. The agent gets back a pending order number and any review flags.
  Then, as the operator:

  ```bash
  dotnet publish/erp-mcp.dll orders show SO-100061
  dotnet publish/erp-mcp.dll orders approve SO-100061 --by you@example.com
  ```

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| Client says the server exited immediately | Run the same command in a terminal and read stderr. Usually Postgres isn't running or there's an unknown scope in `Security__Scopes` (exit code 1). |
| `create_draft_order` is missing | Not granted. Add `orders:draft` to `Security__Scopes`. |
| A tool returns `isError` with "is not an argument of" | The model misspelled an argument name. The message tells it the right one. |
| What did the agent do? | `dotnet publish/erp-mcp.dll audit` ([audit.md](audit.md)) |
