using System.Security.Claims;
using ErpMcp.Server.Security;

namespace ErpMcp.UnitTests.Security;

public class CallerIdentityTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), authenticationType: "Bearer"));

    [Fact]
    public void Prefers_email_and_normalises_case() =>
        CallerIdentity.PersonFrom(User(("sub", "u-1"), ("email", "Alice@Example.com"))).ShouldBe("alice@example.com");

    [Fact]
    public void Falls_back_to_subject() =>
        CallerIdentity.PersonFrom(User(("sub", "auth0|12345"))).ShouldBe("auth0|12345");

    [Fact]
    public void Skips_unsafe_values() =>
        CallerIdentity.PersonFrom(User(("email", "a b<script>@x"), ("sub", "u-42"))).ShouldBe("u-42");

    [Fact]
    public void Never_lets_a_token_claim_an_agent_identity() =>
        CallerIdentity.PersonFrom(User(("email", "agent:claude"))).ShouldBeNull();

    [Fact]
    public void No_user_over_stdio() =>
        CallerIdentity.PersonFrom(null).ShouldBeNull();
}
