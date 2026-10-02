using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace ErpMcp.IntegrationTests.EndToEnd;

public class McpStructuredOutputAndPromptsTests(PostgresFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_tool_publishes_an_object_output_schema()
    {
        var client = await db.GetMcpClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: Ct);

        tools.ShouldAllBe(t => t.ProtocolTool.OutputSchema.HasValue
            && t.ProtocolTool.OutputSchema.Value.GetProperty("type").GetString() == "object");
    }

    [Fact]
    public async Task Output_schema_advertises_snake_case_enum_values()
    {
        var client = await db.GetMcpClientAsync();
        var getOrder = (await client.ListToolsAsync(cancellationToken: Ct)).Single(t => t.Name == "get_order");

        var statuses = getOrder.ProtocolTool.OutputSchema!.Value
            .GetProperty("properties").GetProperty("status").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString());

        statuses.ShouldBe(["pending_approval", "approved", "rejected", "fulfilled", "cancelled"], ignoreOrder: true);
    }

    [Fact]
    public async Task Results_carry_structured_content()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("list_product_categories", cancellationToken: Ct);

        result.StructuredContent.ShouldNotBeNull();
        var categories = result.StructuredContent.Value.GetProperty("categories");
        categories.GetArrayLength().ShouldBe(7);
        categories[0].GetProperty("name").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Prompts_follow_scopes()
    {
        var readOnly = await (await db.GetMcpClientAsync("read")).ListPromptsAsync(cancellationToken: Ct);
        var full = await (await db.GetMcpClientAsync()).ListPromptsAsync(cancellationToken: Ct);

        readOnly.Select(p => p.Name).ShouldBe(["customer_account_review", "review_low_stock"], ignoreOrder: true);
        full.Select(p => p.Name).ShouldContain("draft_order_from_request");
    }

    [Fact]
    public async Task Prompt_with_invalid_argument_is_an_invalid_params_error()
    {
        var client = await db.GetMcpClientAsync();

        var ex = await Should.ThrowAsync<McpProtocolException>(() => client.GetPromptAsync(
            "customer_account_review", new Dictionary<string, object?> { ["customerCode"] = "nobody" }, cancellationToken: Ct).AsTask());

        ex.ErrorCode.ShouldBe(McpErrorCode.InvalidParams);
    }

    [Fact]
    public async Task Prompt_renders_with_validated_arguments()
    {
        var client = await db.GetMcpClientAsync();

        var prompt = await client.GetPromptAsync(
            "review_low_stock", new Dictionary<string, object?> { ["warehouseCode"] = "wh-man" }, cancellationToken: Ct);

        prompt.Messages.ShouldHaveSingleItem().Content.ShouldBeOfType<TextContentBlock>().Text.ShouldContain("WH-MAN");
    }

    [Fact]
    public async Task Guide_resource_lists_only_granted_tools()
    {
        var client = await db.GetMcpClientAsync("read");

        var resource = await client.ReadResourceAsync("erp://guide", cancellationToken: Ct);

        var text = resource.Contents.OfType<TextResourceContents>().Single().Text;
        text.ShouldContain("`get_stock_level`");
        text.ShouldNotContain("`create_draft_order`");
    }
}
