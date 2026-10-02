using System.Text.Json;
using ErpMcp.Application.Auditing;
using ModelContextProtocol.Protocol;

namespace ErpMcp.IntegrationTests.EndToEnd;

/// <summary>Every tool call through the real server leaves exactly one audit event.</summary>
public class McpAuditTests(PostgresFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IAuditLog _audit = db.GetService<IAuditLog>();

    [Fact]
    public async Task Successful_read_is_audited_with_arguments_and_client_identity()
    {
        var marker = Unique();
        var client = await db.GetMcpClientAsync();

        await client.CallToolAsync("search_products", new Dictionary<string, object?> { ["query"] = marker }, cancellationToken: Ct);

        var entry = await SingleEventAsync("search_products", marker);
        entry.Channel.ShouldBe("mcp");
        entry.Outcome.ShouldBe(AuditOutcome.Success);
        entry.Actor.ShouldStartWith("agent:");
        entry.ClientName.ShouldNotBeNullOrEmpty();
        entry.SessionId.ShouldNotBeNullOrEmpty();
        entry.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Refused_calls_are_audited_with_their_reason()
    {
        var marker = Unique();
        var readOnly = await db.GetMcpClientAsync("read");
        var full = await db.GetMcpClientAsync();

        await readOnly.CallToolAsync("create_draft_order", new Dictionary<string, object?>
        {
            ["customerCode"] = "CUST-0001",
            ["lines"] = new[] { new { sku = "BEV-0001", quantity = 1 } },
            ["idempotencyKey"] = marker,
        }, cancellationToken: Ct);
        await full.CallToolAsync("search_products", new Dictionary<string, object?> { ["qury"] = marker }, cancellationToken: Ct);
        await full.CallToolAsync("search_customers", new Dictionary<string, object?> { ["query"] = marker, ["status"] = "vip" }, cancellationToken: Ct);

        (await SingleEventAsync("create_draft_order", marker)).Outcome.ShouldBe(AuditOutcome.Denied);
        (await SingleEventAsync("search_products", marker)).Outcome.ShouldBe(AuditOutcome.Invalid);
        var invalidStatus = await SingleEventAsync("search_customers", marker);
        invalidStatus.Outcome.ShouldBe(AuditOutcome.Invalid);
        invalidStatus.ErrorMessage!.ShouldContain("must be one of");
    }

    [Fact]
    public async Task Draft_order_audit_links_to_the_created_order_and_redacts_notes()
    {
        var marker = Unique();
        var client = await db.GetMcpClientAsync();

        var result = await client.CallToolAsync("create_draft_order", new Dictionary<string, object?>
        {
            ["customerCode"] = "CUST-0019",
            ["lines"] = new[] { new { sku = "DRY-0003", quantity = 2 } },
            ["notes"] = "Gate code 4321",
            ["idempotencyKey"] = marker,
        }, cancellationToken: Ct);

        var orderNumber = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text)
            .RootElement.GetProperty("order").GetProperty("orderNumber").GetString();

        var entry = await SingleEventAsync("create_draft_order", marker);
        entry.Outcome.ShouldBe(AuditOutcome.Success);
        entry.EntityRef.ShouldBe(orderNumber);
        entry.ArgumentsJson.ShouldContain("[redacted]");
        entry.ArgumentsJson.ShouldNotContain("4321");
    }

    private static string Unique() => "audit-" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Finds the one event for this test by a unique marker in its arguments.</summary>
    private async Task<AuditEntry> SingleEventAsync(string action, string marker)
    {
        var events = await _audit.ListRecentAsync(new AuditQuery(500, Action: action), Ct);
        return events.Where(e => e.Entry.ArgumentsJson.Contains(marker, StringComparison.Ordinal))
            .ShouldHaveSingleItem().Entry;
    }
}
