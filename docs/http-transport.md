# HTTP transport and OAuth

`erp-mcp` (no arguments) speaks **stdio**: one local client, no user identity, scopes from config.
`erp-mcp http` speaks **Streamable HTTP**, for remote and shared deployments. There, every request
carries an OAuth access token, and the server knows *which person* the agent is working for.

## How a client connects

```
Client                                  erp-mcp http                      Authorization server
  │ POST /mcp (no token)                     │                                     │
  │ ───────────────────────────────────────▶ │                                     │
  │ 401  WWW-Authenticate: Bearer            │                                     │
  │      resource_metadata=".../.well-known/oauth-protected-resource/mcp"          │
  │ ◀─────────────────────────────────────── │                                     │
  │ GET /.well-known/oauth-protected-resource/mcp                                  │
  │ ───────────────────────────────────────▶ │                                     │
  │ { authorization_servers, scopes_supported: [catalog:read, …, orders:draft] }   │
  │ ◀─────────────────────────────────────── │                                     │
  │ OAuth 2.1 + PKCE: the user signs in and consents to scopes ──────────────────▶ │
  │ ◀──────────────────────────────────────────────────────────────── access token │
  │ POST /mcp  Authorization: Bearer <token> │                                     │
  │ ───────────────────────────────────────▶ │ validates signature, issuer,        │
  │                                          │ audience and expiry (JWKS from the  │
  │                                          │ authority's discovery document)     │
```

This follows the MCP authorization spec: the server is an OAuth **resource server** and
publishes [RFC 9728](https://www.rfc-editor.org/rfc/rfc9728) protected-resource metadata. Login,
consent and token issuance belong to your identity provider (Entra ID, Auth0, Keycloak, Okta…).

## Effective scopes = server ceiling ∩ token scopes

| | Example |
| --- | --- |
| Server ceiling (`Security:Scopes`, set by the operator) | `read orders:draft` |
| Token `scope` / `scp` claim (consented by the user) | `catalog:read orders:draft openid` |
| **Effective for this request** | `catalog:read orders:draft` |

A token can never exceed what the operator allowed, and the server can never exceed what the
user consented to. Unrelated token scopes (`openid`, `profile`) are ignored. The `read`
shorthand works in tokens too. Tools, prompts and the `erp://guide` resource all use the
effective set, and `get_server_info` reports it.

## Acting on behalf of a person

The caller's identity comes from the first safe value among `email`, `preferred_username`,
`upn` and `sub`. It is:

- stored on drafts as `onBehalfOf` (`createdBy` stays `agent:<client>`)
- stored on every audit event (`on_behalf_of`), and `erp-mcp audit --actor alice@example.com`
  finds both
- **barred from approving their own request.** If Alice asks an agent to draft an order, someone
  else has to approve it.

A token can't claim an `agent:` identity.

## Configuration

| Setting | Example | Notes |
| --- | --- | --- |
| `Http:Url` | `http://0.0.0.0:5100` | Listen address |
| `Http:PublicUrl` | `https://erp-mcp.example.com` | External base URL; the resource id is `{PublicUrl}/mcp` |
| `Auth:Authority` | `https://login.example.com/` | Issuer; keys come from its OIDC discovery |
| `Auth:Audience` | `https://erp-mcp.example.com/mcp` | Required `aud` |
| `Auth:DevSigningKey` | 32+ bytes | **Local development only.** HS256, issuer `erp-mcp-dev` |
| `Security:Scopes` | `read orders:draft` | The ceiling |

Exactly one of `Authority` or `DevSigningKey` must be set, otherwise start-up fails. Run behind
TLS in production: put a reverse proxy in front and set `PublicUrl` to the https address.
`/healthz` is unauthenticated, and everything under `/mcp` requires a valid token.

## Try it locally

```bash
docker compose --profile http up -d --wait erp-mcp-http        # http://localhost:5100/mcp, dev key
TOKEN=$(python3 scripts/dev_token.py --scope "read orders:draft" --email alice@example.com)

# Claude Code
claude mcp add --transport http erp-remote http://localhost:5100/mcp --header "Authorization: Bearer $TOKEN"

# MCP Inspector: choose "Streamable HTTP", URL http://localhost:5100/mcp, add the Authorization header
npx @modelcontextprotocol/inspector
```

Or without Docker:

```bash
dotnet run --project src/ErpMcp.Server -- http \
  --Auth:Audience=erp-mcp --Auth:DevSigningKey=local-dev-signing-key-change-me-0123456789
```

## Sessions and audit

The HTTP transport is stateful. Each client gets an MCP session id, and that id is recorded as
the audit `session_id`, so one agent conversation can be traced end to end. Over stdio the
per-process id is used instead.
