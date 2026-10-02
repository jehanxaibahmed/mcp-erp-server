namespace ErpMcp.Server.Security;

/// <summary>The scopes this server instance was started with. Fixed for the life of the process.</summary>
public sealed record GrantedScopes(IReadOnlySet<string> Scopes)
{
    public bool Contains(string scope) => Scopes.Contains(scope);
}
