using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace ErpMcp.IntegrationTests.EndToEnd;

public class McpSecurityTests(PostgresFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_only_default_hides_the_draft_tool()
    {
        var client = await db.GetMcpClientAsync("read");

        var tools = await client.ListToolsAsync(cancellationToken: Ct);

        tools.Select(t => t.Name).ShouldNotContain("create_draft_order");
        tools.ShouldAllBe(t => t.ProtocolTool.Annotations!.ReadOnlyHint == true);
    }

    [Fact]
    public async Task Calling_an_ungranted_tool_by_name_is_refused()
    {
        var client = await db.GetMcpClientAsync("read");

        var result = await client.CallToolAsync("create_draft_order", new Dictionary<string, object?>
        {
            ["customerCode"] = "CUST-0001",
            ["lines"] = new[] { new { sku = "BEV-0001", quantity = 1 } },
        }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("requires the 'orders:draft' scope");
    }

    [Fact]
    public async Task Narrow_grant_exposes_only_matching_tools()
    {
        var client = await db.GetMcpClientAsync("orders:read");

        var tools = await client.ListToolsAsync(cancellationToken: Ct);

        tools.Select(t => t.Name).ShouldBe(["get_order", "get_server_info", "list_orders"], ignoreOrder: true);
    }

    [Fact]
    public async Task Server_info_reports_granted_scopes()
    {
        var client = await db.GetMcpClientAsync("orders:read");

        var result = await client.CallToolAsync("get_server_info", cancellationToken: Ct);

        var scopes = JsonDocument.Parse(Text(result)).RootElement.GetProperty("grantedScopes").EnumerateArray().Select(e => e.GetString());
        scopes.ShouldBe(["orders:read"]);
    }

    [Fact]
    public async Task Misspelled_argument_is_rejected_with_a_suggestion_instead_of_widening_the_query()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("list_orders", new Dictionary<string, object?> { ["customer_code"] = "CUST-0001" }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        Text(result).ShouldBe("Invalid 'customer_code': is not an argument of 'list_orders'. Did you mean 'customerCode'?");
    }

    [Fact]
    public async Task Oversized_arguments_are_rejected()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("search_products", new Dictionary<string, object?> { ["query"] = new string('x', 40_000) }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("the limit is");
    }

    private static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;
}
