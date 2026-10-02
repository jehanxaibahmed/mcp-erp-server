using ErpMcp.Application.Security;
using ErpMcp.Server.Security;
using ErpMcp.Server.Tools;

namespace ErpMcp.UnitTests.Security;

public class ScopeCatalogTests
{
    private static readonly ScopeCatalog Catalog = ScopeCatalog.FromAssembly(typeof(ServerInfoTools).Assembly);

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
        Catalog.RequiredScopeForTool(tool).ShouldBe(scope);

    [Fact]
    public void Only_the_draft_tool_needs_a_write_scope() =>
        Catalog.ScopeByTool.Where(kv => kv.Value == Scopes.OrdersDraft).Select(kv => kv.Key).ShouldBe(["create_draft_order"]);

    [Fact]
    public void Read_only_grant_allows_reads_but_not_drafting()
    {
        Catalog.IsToolAllowed("get_stock_level", Scopes.ReadOnly).ShouldBeTrue();
        Catalog.IsToolAllowed("create_draft_order", Scopes.ReadOnly).ShouldBeFalse();
    }

    [Fact]
    public void Scope_free_tools_are_always_allowed() =>
        Catalog.IsToolAllowed("get_server_info", new HashSet<string>()).ShouldBeTrue();

    [Theory]
    [InlineData("review_low_stock", Scopes.InventoryRead)]
    [InlineData("customer_account_review", Scopes.CustomersRead)]
    [InlineData("draft_order_from_request", Scopes.OrdersDraft)]
    public void Maps_prompts_to_scopes(string prompt, string scope) =>
        Catalog.RequiredScopeForPrompt(prompt).ShouldBe(scope);

    [Fact]
    public void Draft_prompt_is_hidden_under_read_only_grant() =>
        Catalog.IsPromptAllowed("draft_order_from_request", Scopes.ReadOnly).ShouldBeFalse();
}
