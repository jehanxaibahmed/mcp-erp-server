#!/usr/bin/env bash
# End-to-end demo: an agent drafts an order over MCP, a person reviews, approves and fulfils it,
# and the audit trail shows every step. Needs Docker and the .NET 10 SDK.
set -euo pipefail
cd "$(dirname "$0")/.."

# Keep CLI output and "error:" lines; drop framework log noise.
erp() { dotnet run --no-build --project src/ErpMcp.Server -- "$@" 2>&1 | grep -vE '^(info|warn|dbug): |^      ' || true; }
tool() { MCP_TOOL_SCOPES="$1"; shift; Security__Scopes="$MCP_TOOL_SCOPES" python3 scripts/mcp_smoke.py "$@" 2>/dev/null | sed 1d; }
step() { printf '\n\033[1m── %s\033[0m\n' "$*"; }
text() { python3 -c 'import json,sys; r=json.load(sys.stdin); print(json.dumps(r.get("structuredContent") or r, indent=2))'; }

step "Start PostgreSQL, build, migrate and seed"
docker compose up -d --wait >/dev/null
dotnet build -v q ErpMcp.slnx >/dev/null
erp migrate --seed

step "Agent (read-only): is BEV-0005 in stock?"
tool read get_stock_level '{"sku":"BEV-0005"}' | text | head -12

step "Agent (read-only) tries to draft. The tool isn't even offered, and a call by name is refused"
tool read create_draft_order '{"customerCode":"CUST-0004","lines":[{"sku":"BEV-0005","quantity":6}]}'

step "Agent (read orders:draft) drafts an order for Riverside Care Home"
KEY="demo-$(date +%s)"
DRAFT=$(tool "read orders:draft" create_draft_order \
  "{\"customerCode\":\"CUST-0004\",\"lines\":[{\"sku\":\"BEV-0005\",\"quantity\":6},{\"sku\":\"DAI-0005\",\"quantity\":2}],\"notes\":\"Deliver to kitchen\",\"idempotencyKey\":\"$KEY\"}")
echo "$DRAFT" | python3 -c 'import json,sys; r=json.load(sys.stdin)["structuredContent"]; print(r["message"]); print("review flags:", r["order"]["reviewFlags"] or "none")'
ORDER=$(echo "$DRAFT" | python3 -c 'import json,sys; print(json.load(sys.stdin)["structuredContent"]["order"]["orderNumber"])')

step "Agent retries with the same idempotency key (e.g. after a timeout)"
tool "read orders:draft" create_draft_order \
  "{\"customerCode\":\"CUST-0004\",\"lines\":[{\"sku\":\"BEV-0005\",\"quantity\":6},{\"sku\":\"DAI-0005\",\"quantity\":2}],\"notes\":\"Deliver to kitchen\",\"idempotencyKey\":\"$KEY\"}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["structuredContent"]["message"])'

step "Operator reviews the queue"
erp orders pending --limit 5

step "An agent identity tries to approve: refused"
erp orders approve "$ORDER" --by agent:helper

step "Operator approves; stock is reserved"
erp orders approve "$ORDER" --by ops.lead@example.com

step "Warehouse fulfils; reserved stock ships"
erp orders fulfil "$ORDER" --by warehouse@example.com
erp orders show "$ORDER"

step "Audit trail for this order"
erp audit --limit 12 | awk -v order="$ORDER" 'NR == 1 || index($0, order) || /denied|rejected/' | head -9
