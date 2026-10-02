using ErpMcp.Application.Security;
using ErpMcp.Server.Security;
using ErpMcp.Server.Tools;

namespace ErpMcp.UnitTests.Security;

public class ToolCatalogTests
{
    private static readonly ToolCatalog Catalog = ToolCatalog.FromAssembly(typeof(ServerInfoTools).Assembly);

    [Fact]
    public void Every_tool_declares_a_known_scope() =>
        // FromAssembly throws if any tool lacks [RequiresScope] or names an unknown scope.
        Catalog.ScopeByTool.Count.ShouldBe(11);

    [Theory]
    [InlineData("search_customers", Scopes.CustomersRead)]
    [InlineData("get_product", Scopes.CatalogRead)]
    [InlineData("list_low_stock", Scopes.InventoryRead)]
    [InlineData("get_order", Scopes.OrdersRead)]
    [InlineData("create_draft_order", Scopes.OrdersDraft)]
    [InlineData("get_server_info", RequiresScopeAttribute.None)]
    public void Maps_tools_to_scopes(string tool, string scope) =>
        Catalog.RequiredScope(tool).ShouldBe(scope);

    [Fact]
    public void Only_the_draft_tool_needs_a_write_scope() =>
        Catalog.ScopeByTool.Where(kv => kv.Value == Scopes.OrdersDraft).Select(kv => kv.Key).ShouldBe(["create_draft_order"]);

    [Fact]
    public void Read_only_grant_allows_reads_but_not_drafting()
    {
        Catalog.IsAllowed("get_stock_level", Scopes.ReadOnly).ShouldBeTrue();
        Catalog.IsAllowed("create_draft_order", Scopes.ReadOnly).ShouldBeFalse();
    }

    [Fact]
    public void Scope_free_tools_are_always_allowed() =>
        Catalog.IsAllowed("get_server_info", new HashSet<string>()).ShouldBeTrue();
}
