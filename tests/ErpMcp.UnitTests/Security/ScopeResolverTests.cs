using System.Security.Claims;
using ErpMcp.Application.Security;
using ErpMcp.Server.Security;

namespace ErpMcp.UnitTests.Security;

public class ScopeResolverTests
{
    private static readonly GrantedScopes Ceiling = new(Scopes.Parse("read orders:draft"));

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), authenticationType: "Bearer"));

    [Fact]
    public void Stdio_without_a_user_gets_the_server_grant() =>
        new ScopeResolver(Ceiling, requireAuthenticatedUser: false).Resolve(null).ShouldBe(Ceiling.Scopes, ignoreOrder: true);

    [Fact]
    public void Http_without_a_user_gets_nothing() =>
        new ScopeResolver(Ceiling, requireAuthenticatedUser: true).Resolve(new ClaimsPrincipal()).ShouldBeEmpty();

    [Fact]
    public void Token_narrows_the_server_grant() =>
        new ScopeResolver(Ceiling, true).Resolve(User(("scope", "catalog:read orders:read")))
            .ShouldBe(["catalog:read", "orders:read"], ignoreOrder: true);

    [Fact]
    public void Token_cannot_exceed_the_server_grant()
    {
        var readOnlyServer = new ScopeResolver(new GrantedScopes(Scopes.ReadOnly), true);

        readOnlyServer.Resolve(User(("scope", "read orders:draft"))).ShouldNotContain(Scopes.OrdersDraft);
    }

    [Fact]
    public void Unrelated_and_unknown_token_scopes_are_ignored() =>
        new ScopeResolver(Ceiling, true).Resolve(User(("scope", "openid profile orders:approve catalog:read")))
            .ShouldBe(["catalog:read"]);

    [Fact]
    public void Supports_scp_claims_and_the_read_shorthand() =>
        new ScopeResolver(Ceiling, true).Resolve(User(("scp", "read"))).ShouldBe(Scopes.ReadOnly, ignoreOrder: true);
}
