using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace ErpMcp.IntegrationTests.EndToEnd;

public class McpDraftOrderTests(PostgresFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Draft_tool_is_the_only_write_tool_and_is_marked_non_destructive()
    {
        var client = await db.GetMcpClientAsync();
        var tools = await client.ListToolsAsync(cancellationToken: Ct);

        var writeTools = tools.Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint != true).ToList();
        var draft = writeTools.ShouldHaveSingleItem();
        draft.Name.ShouldBe("create_draft_order");
        draft.ProtocolTool.Annotations!.DestructiveHint.ShouldBe(false);
    }

    [Fact]
    public async Task Agent_can_draft_but_order_stays_pending_and_is_attributed_to_the_client()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("create_draft_order", new Dictionary<string, object?>
        {
            ["customerCode"] = "CUST-0002",
            ["lines"] = new[] { new { sku = "DAI-0001", quantity = 24 }, new { sku = "DRY-0005", quantity = 6 } },
            ["notes"] = "Kitchen entrance",
        }, cancellationToken: Ct);

        result.IsError.ShouldNotBe(true);
        var json = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        var order = json.GetProperty("order");
        order.GetProperty("status").GetString().ShouldBe("pending_approval");
        order.GetProperty("createdBy").GetString()!.ShouldStartWith("agent:");
        order.GetProperty("lines").GetArrayLength().ShouldBe(2);
        json.GetProperty("message").GetString()!.ShouldContain("pending human approval");
    }

    [Fact]
    public async Task Business_rule_violations_come_back_as_tool_errors()
    {
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("create_draft_order", new Dictionary<string, object?>
        {
            ["customerCode"] = "CUST-0016",
            ["lines"] = new[] { new { sku = "BEV-0001", quantity = 1 } },
        }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("Customer CUST-0016 is closed and cannot place orders.");
    }
}
