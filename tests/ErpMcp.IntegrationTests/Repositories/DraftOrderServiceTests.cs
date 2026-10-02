using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.IntegrationTests.Repositories;

public class DraftOrderServiceTests(PostgresFixture db)
{
    private const string Agent = "agent:tests";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DraftOrderService Drafts => db.GetService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<DraftOrderService>();

    private static DraftOrderRequest Request(string? key = null, params (string Sku, int Qty)[] lines) =>
        new("CUST-0005", lines.Length == 0 ? [new("BEV-0002", 3)] : lines.Select(l => new DraftOrderLineRequest(l.Sku, l.Qty)).ToList(), "Test order", key);

    private static string NewKey() => "test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Creates_pending_agent_order_priced_from_catalogue()
    {
        var result = await Drafts.CreateAsync(Request(null, ("bev-0002", 3), ("DRY-0001", 2)), Agent, Ct);

        var order = result.Order;
        result.AlreadyExisted.ShouldBeFalse();
        order.Status.ShouldBe(OrderStatus.PendingApproval);
        order.Source.ShouldBe(OrderSource.Agent);
        order.CreatedBy.ShouldBe(Agent);
        order.DecidedAt.ShouldBeNull();
        order.Lines.Select(l => (l.Sku, l.Quantity, l.UnitPrice)).ShouldBe([("BEV-0002", 3, 21.75m), ("DRY-0001", 2, 18.40m)]);
        order.TotalAmount.ShouldBe(3 * 21.75m + 2 * 18.40m);
    }

    [Fact]
    public async Task Records_review_flags_for_the_approver()
    {
        var result = await Drafts.CreateAsync(Request(null, ("BEV-0001", 9_000)), Agent, Ct);

        result.Order.ReviewFlags.ShouldContain(f => f.StartsWith("Insufficient stock for BEV-0001", StringComparison.Ordinal));
        result.Order.ReviewFlags.ShouldContain(f => f.StartsWith("Exceeds credit limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejects_on_hold_customer()
    {
        var request = Request() with { CustomerCode = "CUST-0006" };

        await Should.ThrowAsync<DomainRuleViolationException>(() => Drafts.CreateAsync(request, Agent, Ct));
    }

    [Fact]
    public async Task Rejects_unknown_customer() =>
        await Should.ThrowAsync<NotFoundException>(() => Drafts.CreateAsync(Request() with { CustomerCode = "CUST-9999" }, Agent, Ct));

    [Fact]
    public async Task Reports_which_line_is_malformed()
    {
        var ex = await Should.ThrowAsync<InputValidationException>(() =>
            Drafts.CreateAsync(Request(null, ("BEV-0001", 1), ("nonsense", 1)), Agent, Ct));

        ex.Field.ShouldBe("lines[1].sku");
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_same_order()
    {
        var key = NewKey();

        var first = await Drafts.CreateAsync(Request(key), Agent, Ct);
        var second = await Drafts.CreateAsync(Request(key), Agent, Ct);

        second.AlreadyExisted.ShouldBeTrue();
        second.Order.OrderNumber.ShouldBe(first.Order.OrderNumber);
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_order_is_rejected()
    {
        var key = NewKey();
        await Drafts.CreateAsync(Request(key, ("BEV-0002", 3)), Agent, Ct);

        var ex = await Should.ThrowAsync<InputValidationException>(() =>
            Drafts.CreateAsync(Request(key, ("BEV-0002", 4)), Agent, Ct));

        ex.Field.ShouldBe("idempotencyKey");
    }

    [Fact]
    public async Task Concurrent_retries_with_one_key_create_exactly_one_order()
    {
        var key = NewKey();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Drafts.CreateAsync(Request(key), Agent, Ct)));

        results.Select(r => r.Order.OrderNumber).Distinct().ShouldHaveSingleItem();
        results.Count(r => !r.AlreadyExisted).ShouldBe(1);
    }
}
