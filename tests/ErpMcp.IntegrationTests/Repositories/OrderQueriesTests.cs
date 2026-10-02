using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Orders;

namespace ErpMcp.IntegrationTests.Repositories;

public class OrderQueriesTests(PostgresFixture db)
{
    private readonly OrderQueries _orders = db.GetService<OrderQueries>();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_returns_newest_first()
    {
        var result = await _orders.ListAsync(null, null, null, null, 50, null, Ct);

        result.Items.Select(o => o.CreatedAt).ShouldBe(result.Items.Select(o => o.CreatedAt).OrderDescending());
    }

    [Fact]
    public async Task List_filters_by_status_and_customer()
    {
        var pending = await _orders.ListAsync(null, "pending_approval", null, null, null, null, Ct);
        pending.Items.ShouldAllBe(o => o.Status == OrderStatus.PendingApproval);

        var customer = pending.Items[0].CustomerCode;
        var forCustomer = await _orders.ListAsync(customer, null, null, null, 100, null, Ct);
        forCustomer.Items.ShouldAllBe(o => o.CustomerCode == customer);
    }

    [Fact]
    public async Task List_date_range_is_inclusive_of_both_days()
    {
        var newest = (await _orders.ListAsync(null, null, null, null, 1, null, Ct)).Items.Single();
        var day = DateOnly.FromDateTime(newest.CreatedAt.UtcDateTime);

        var result = await _orders.ListAsync(null, null, day, day, 100, null, Ct);

        result.Items.ShouldContain(o => o.OrderNumber == newest.OrderNumber);
        result.Items.ShouldAllBe(o => DateOnly.FromDateTime(o.CreatedAt.UtcDateTime) == day);
    }

    [Fact]
    public async Task List_rejects_inverted_date_range() =>
        await Should.ThrowAsync<InputValidationException>(() => _orders.ListAsync(
            null, null, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null, null, Ct));

    [Fact]
    public async Task Get_returns_lines_that_add_up_to_the_total()
    {
        var order = await _orders.GetAsync("SO-100001", Ct);

        order.Lines.ShouldNotBeEmpty();
        order.Lines.Sum(l => l.LineTotal).ShouldBe(order.TotalAmount);
        order.Lines.Select(l => l.LineNumber).ShouldBe(Enumerable.Range(1, order.Lines.Count));
    }

    [Fact]
    public async Task Rejected_orders_carry_reason_and_decision()
    {
        var rejected = (await _orders.ListAsync(null, "rejected", null, null, 1, null, Ct)).Items.Single();
        var order = await _orders.GetAsync(rejected.OrderNumber, Ct);

        order.RejectionReason.ShouldNotBeNullOrWhiteSpace();
        order.DecidedBy.ShouldNotBeNull();
        order.DecidedAt.ShouldNotBeNull();
    }
}
