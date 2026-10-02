using ErpMcp.Application.Common;
using ErpMcp.Application.Security;

namespace ErpMcp.UnitTests.Security;

public class ScopesTests
{
    [Fact]
    public void Read_shorthand_expands_to_every_read_scope_and_no_write_scope()
    {
        var granted = Scopes.Parse("read");

        granted.ShouldBe(Scopes.ReadOnly, ignoreOrder: true);
        granted.ShouldNotContain(Scopes.OrdersDraft);
    }

    [Theory]
    [InlineData("read orders:draft")]
    [InlineData("read,orders:draft")]
    [InlineData("  READ ;  Orders:Draft ")]
    public void Accepts_space_comma_or_semicolon_separated_lists(string value) =>
        Scopes.Parse(value).ShouldBe(Scopes.All, ignoreOrder: true);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_grants_nothing(string? value) =>
        Scopes.Parse(value).ShouldBeEmpty();

    [Fact]
    public void Unknown_scope_fails_loudly()
    {
        var ex = Should.Throw<InputValidationException>(() => Scopes.Parse("read orders:write"));
        ex.Message.ShouldContain("orders:write");
        ex.Message.ShouldContain("orders:draft");
    }

    [Fact]
    public void Wildcards_are_not_supported() =>
        Should.Throw<InputValidationException>(() => Scopes.Parse("*"));
}
