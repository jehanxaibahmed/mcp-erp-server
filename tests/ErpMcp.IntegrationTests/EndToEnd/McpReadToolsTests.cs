using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ErpMcp.IntegrationTests.EndToEnd;

/// <summary>Drives the compiled server over stdio with the official MCP client.</summary>
public class McpReadToolsTests(PostgresFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lists_read_tools_annotated_as_read_only()
    {
        var client = await db.GetMcpClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: Ct);

        tools.Select(t => t.Name).ShouldBe(
            [
                "get_customer", "get_order", "get_product", "get_server_info", "get_stock_level",
                "list_low_stock", "list_orders", "list_product_categories", "search_customers", "search_products",
            ],
            ignoreOrder: true);
        tools.ShouldAllBe(t => t.ProtocolTool.Annotations!.ReadOnlyHint == true);
    }

    [Fact]
    public async Task Get_stock_level_returns_structured_json()
    {
        var json = await CallAsync("get_stock_level", new() { ["sku"] = "dai-0002" });

        json.GetProperty("sku").GetString().ShouldBe("DAI-0002");
        json.GetProperty("warehouses").GetArrayLength().ShouldBe(3);
        json.GetProperty("warehouses")[0].TryGetProperty("quantityAvailable", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Enums_are_serialised_as_snake_case()
    {
        var json = await CallAsync("list_orders", new() { ["status"] = "pending_approval", ["limit"] = 1 });

        json.GetProperty("items")[0].GetProperty("status").GetString().ShouldBe("pending_approval");
    }

    [Fact]
    public async Task Invalid_arguments_return_a_tool_error_the_agent_can_act_on()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("get_product", new Dictionary<string, object?> { ["sku"] = "coffee" }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        TextOf(result).ShouldBe("Invalid 'sku': expected a code like 'BEV-0001'.");
    }

    [Fact]
    public async Task Unknown_records_return_not_found_tool_error()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("get_order", new Dictionary<string, object?> { ["orderNumber"] = "SO-999999" }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        TextOf(result).ShouldBe("Order 'SO-999999' was not found.");
    }

    private async Task<JsonElement> CallAsync(string tool, Dictionary<string, object?> arguments)
    {
        var client = await db.GetMcpClientAsync();
        var result = await client.CallToolAsync(tool, arguments, cancellationToken: Ct);
        result.IsError.ShouldNotBe(true, TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static string TextOf(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}
