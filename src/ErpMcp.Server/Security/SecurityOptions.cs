using ErpMcp.Application.Security;

namespace ErpMcp.Server.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Space-separated scopes granted to this server instance, e.g.
    /// <c>"read orders:draft"</c>. Defaults to read-only.
    /// </summary>
    public string Scopes { get; set; } = "read";

    /// <summary>Upper bound on the serialised size of one tool call's arguments.</summary>
    public int MaxArgumentBytes { get; set; } = 32 * 1024;

    public IReadOnlySet<string> GrantedScopes() => Application.Security.Scopes.Parse(Scopes);
}
