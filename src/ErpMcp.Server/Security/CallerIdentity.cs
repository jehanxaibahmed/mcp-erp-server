using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ErpMcp.Server.Security;

/// <summary>Who a request comes from, as far as the transport can tell.</summary>
public static partial class CallerIdentity
{
    private static readonly string[] PersonClaims = ["email", "preferred_username", "upn", "sub"];

    /// <summary>
    /// The authenticated person behind an HTTP request, from the first usable identity claim
    /// (<c>email</c>, then <c>preferred_username</c>, <c>upn</c>, <c>sub</c>). Null over stdio, where
    /// there is no user, or if no claim holds a safe identifier.
    /// </summary>
    public static string? PersonFrom(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return PersonClaims
            .Select(type => user.FindFirst(type)?.Value?.Trim().ToLowerInvariant())
            .FirstOrDefault(value => value is not null && SafeIdentifier().IsMatch(value) && !value.StartsWith("agent:", StringComparison.Ordinal));
    }

    [GeneratedRegex("^[a-z0-9._:@+|-]{3,100}$")]
    private static partial Regex SafeIdentifier();
}
