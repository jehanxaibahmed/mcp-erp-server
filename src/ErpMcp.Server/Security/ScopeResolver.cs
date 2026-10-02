using System.Security.Claims;
using ErpMcp.Application.Security;

namespace ErpMcp.Server.Security;

/// <summary>
/// Works out the scopes in force for one request.
/// </summary>
/// <remarks>
/// Over stdio there is no user: the server's configured grant applies. Over HTTP the caller's
/// access token narrows it. Effective scopes are the <em>intersection</em> of the server grant
/// (a ceiling set by the operator) and the token's <c>scope</c>/<c>scp</c> claim. A token can
/// never exceed what the operator allowed, and a server can never exceed what the user consented to.
/// </remarks>
public sealed class ScopeResolver(GrantedScopes serverGrant, bool requireAuthenticatedUser)
{
    public bool RequiresAuthenticatedUser => requireAuthenticatedUser;

    public IReadOnlySet<string> Resolve(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return requireAuthenticatedUser ? new HashSet<string>() : serverGrant.Scopes;
        }

        var tokenScopes = user.FindAll("scope").Concat(user.FindAll("scp"))
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.ToLowerInvariant())
            .SelectMany(s => s == "read" ? Scopes.ReadOnly : (IEnumerable<string>)[s]) // same shorthand as configuration
            .ToHashSet(StringComparer.Ordinal);

        // Tokens commonly carry unrelated scopes (openid, profile…). Ignore them rather than fail.
        return serverGrant.Scopes.Where(tokenScopes.Contains).ToHashSet(StringComparer.Ordinal);
    }
}
