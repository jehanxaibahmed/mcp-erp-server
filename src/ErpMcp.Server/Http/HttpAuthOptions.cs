namespace ErpMcp.Server.Http;

public sealed class HttpOptions
{
    public const string SectionName = "Http";

    /// <summary>Address to listen on.</summary>
    public string Url { get; set; } = "http://localhost:5100";

    /// <summary>
    /// Externally visible base URL (behind a proxy this differs from <see cref="Url"/>). Used as the
    /// OAuth protected-resource identifier: <c>{PublicUrl}/mcp</c>.
    /// </summary>
    public string? PublicUrl { get; set; }
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>OAuth/OIDC authorization server (issuer). Signing keys come from its discovery document.</summary>
    public string? Authority { get; set; }

    /// <summary>Expected <c>aud</c> claim, normally this server's resource URL or client id.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Local development only: validate HS256 tokens signed with this key (≥ 32 bytes) instead of an
    /// authorization server. Tokens must have issuer <see cref="DevIssuer"/>.
    /// </summary>
    public string? DevSigningKey { get; set; }

    public string DevIssuer { get; set; } = "erp-mcp-dev";
}
