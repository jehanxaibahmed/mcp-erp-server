using Dapper;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.IntegrationTests.Repositories;

/// <summary>
/// Stock reservation across the order lifecycle. Each test uses its own product so parallel
/// tests elsewhere cannot change the stock figures being asserted.
/// </summary>
public class OrderFulfilmentTests(PostgresFixture db)
{
    private const string Ops = "ops.lead@example.com";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private T Get<T>() where T : notnull =>
        db.GetService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<T>();

    private async Task<string> DraftAsync(string sku, int quantity) =>
        (await Get<DraftOrderService>().CreateAsync(
            new DraftOrderRequest("CUST-0017", [new(sku, quantity)]), "agent:tests", Ct)).Order.OrderNumber;

    private async Task<(int OnHand, int Reserved)> StockAsync(string sku)
    {
        await using var connection = await db.DataSource.OpenConnectionAsync(Ct);
        return await connection.QuerySingleAsync<(int, int)>("""
            SELECT sum(s.quantity_on_hand)::int, sum(s.quantity_reserved)::int
            FROM erp.stock_levels s JOIN erp.products p ON p.id = s.product_id
            WHERE p.sku = @sku
            """, new { sku });
    }

    [Fact]
    public async Task Approval_reserves_exactly_the_ordered_quantity()
    {
        var before = await StockAsync("CLN-0001");
        var number = await DraftAsync("CLN-0001", 7);

        var order = await Get<OrderApprovalService>().ApproveAsync(number, Ops, Ct);

        order.Allocations.Sum(a => a.Quantity).ShouldBe(7);
        (await StockAsync("CLN-0001")).ShouldBe((before.OnHand, before.Reserved + 7));
    }

    [Fact]
    public async Task Approval_with_insufficient_stock_is_refused_and_changes_nothing()
    {
        var before = await StockAsync("CLN-0002");
        var number = await DraftAsync("CLN-0002", 9_999);

        var ex = await Should.ThrowAsync<DomainRuleViolationException>(() => Get<OrderApprovalService>().ApproveAsync(number, Ops, Ct));

        ex.Message.ShouldContain("insufficient stock (CLN-0002 needs 9999");
        (await StockAsync("CLN-0002")).ShouldBe(before);
        (await Get<OrderQueries>().GetAsync(number, Ct)).Status.ShouldBe(OrderStatus.PendingApproval);
    }

    [Fact]
    public async Task Fulfilment_ships_the_reserved_stock()
    {
        var before = await StockAsync("CLN-0003");
        var number = await DraftAsync("CLN-0003", 4);
        await Get<OrderApprovalService>().ApproveAsync(number, Ops, Ct);

        var order = await Get<OrderFulfilmentService>().FulfilAsync(number, "warehouse@example.com", Ct);

        order.Status.ShouldBe(OrderStatus.Fulfilled);
        order.ClosedBy.ShouldBe("warehouse@example.com");
        order.ClosedAt.ShouldNotBeNull();
        (await StockAsync("CLN-0003")).ShouldBe((before.OnHand - 4, before.Reserved));
    }

    [Fact]
    public async Task Cancelling_an_approved_order_releases_its_reservation()
    {
        var before = await StockAsync("PKG-0001");
        var number = await DraftAsync("PKG-0001", 6);
        await Get<OrderApprovalService>().ApproveAsync(number, Ops, Ct);

        var order = await Get<OrderFulfilmentService>().CancelAsync(number, Ops, "Customer changed their mind", Ct);

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("Customer changed their mind");
        (await StockAsync("PKG-0001")).ShouldBe(before);
    }

    [Fact]
    public async Task Cancelling_a_pending_order_records_the_decision_without_touching_stock()
    {
        var before = await StockAsync("PKG-0002");
        var number = await DraftAsync("PKG-0002", 2);

        var order = await Get<OrderFulfilmentService>().CancelAsync(number, Ops, "Duplicate", Ct);

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.DecidedBy.ShouldBe(Ops);
        (await StockAsync("PKG-0002")).ShouldBe(before);
    }

    [Fact]
    public async Task Pending_orders_cannot_be_fulfilled() =>
        await Should.ThrowAsync<DomainRuleViolationException>(async () =>
            await Get<OrderFulfilmentService>().FulfilAsync(await DraftAsync("FRZ-0001", 1), Ops, Ct));

    [Fact]
    public async Task Agents_cannot_fulfil() =>
        await Should.ThrowAsync<DomainRuleViolationException>(async () =>
            await Get<OrderFulfilmentService>().FulfilAsync(await DraftAsync("FRZ-0002", 1), "agent:helper", Ct));

    [Fact]
    public async Task Concurrent_approvals_never_promise_the_same_stock_twice()
    {
        const string sku = "FRZ-0003";
        var (onHand, reserved) = await StockAsync(sku);
        var available = onHand - reserved;
        var each = available / 2 + 1; // any two of these together exceed what's available

        var numbers = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            numbers.Add(await DraftAsync(sku, each));
        }

        var outcomes = await Task.WhenAll(numbers.Select(async n =>
        {
            try
            {
                await Get<OrderApprovalService>().ApproveAsync(n, Ops, Ct);
                return true;
            }
            catch (DomainRuleViolationException)
            {
                return false;
            }
        }));

        outcomes.Count(approved => approved).ShouldBe(1);
        var after = await StockAsync(sku);
        after.Reserved.ShouldBe(reserved + each);
        after.Reserved.ShouldBeLessThanOrEqualTo(after.OnHand);
    }
}
