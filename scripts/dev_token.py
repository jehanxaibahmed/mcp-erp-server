#!/usr/bin/env python3
"""Mint a short-lived HS256 access token for the HTTP transport's DEVELOPMENT signing key.

Usage:
    python3 scripts/dev_token.py                                   # read scopes, alice@example.com
    python3 scripts/dev_token.py --scope "read orders:draft" --email bob@example.com

Defaults match docker-compose.yml's erp-mcp-http service. Never use a dev key in production:
configure Auth__Authority to point at a real authorization server instead.
"""
import argparse
import base64
import hashlib
import hmac
import json
import time


def b64(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--scope", default="read")
    parser.add_argument("--email", default="alice@example.com")
    parser.add_argument("--audience", default="erp-mcp")
    parser.add_argument("--issuer", default="erp-mcp-dev")
    parser.add_argument("--key", default="local-dev-signing-key-change-me-0123456789")
    parser.add_argument("--minutes", type=int, default=60)
    args = parser.parse_args()

    now = int(time.time())
    header = b64(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload = b64(json.dumps({
        "iss": args.issuer, "aud": args.audience, "sub": args.email, "email": args.email,
        "scope": args.scope, "iat": now, "nbf": now, "exp": now + args.minutes * 60,
    }).encode())
    signature = b64(hmac.new(args.key.encode(), f"{header}.{payload}".encode(), hashlib.sha256).digest())
    print(f"{header}.{payload}.{signature}")


if __name__ == "__main__":
    main()
