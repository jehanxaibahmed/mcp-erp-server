#!/usr/bin/env python3
"""Minimal MCP stdio client for smoke-testing the server without an AI host.

Usage:
    python3 scripts/mcp_smoke.py                      # initialize + list tools
    python3 scripts/mcp_smoke.py get_server_info '{}'  # also call one tool
"""
import json
import subprocess
import sys

CMD = ["dotnet", "run", "--no-build", "--project", "src/ErpMcp.Server", "--"]


def main() -> int:
    proc = subprocess.Popen(CMD, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    next_id = 0

    def request(method, params=None):
        nonlocal next_id
        next_id += 1
        msg = {"jsonrpc": "2.0", "id": next_id, "method": method, "params": params or {}}
        proc.stdin.write(json.dumps(msg) + "\n")
        proc.stdin.flush()
        while True:
            reply = json.loads(proc.stdout.readline())
            if reply.get("id") == next_id:
                return reply

    request("initialize", {
        "protocolVersion": "2025-06-18",
        "capabilities": {},
        "clientInfo": {"name": "mcp-smoke", "version": "1.0"},
    })
    proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
    proc.stdin.flush()

    tools = request("tools/list")["result"]["tools"]
    print("tools:", ", ".join(t["name"] for t in tools))

    if len(sys.argv) >= 2:
        args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
        reply = request("tools/call", {"name": sys.argv[1], "arguments": args})
        print(json.dumps(reply.get("result", reply.get("error")), indent=2))

    proc.stdin.close()
    proc.wait(timeout=10)
    return 0


if __name__ == "__main__":
    sys.exit(main())
