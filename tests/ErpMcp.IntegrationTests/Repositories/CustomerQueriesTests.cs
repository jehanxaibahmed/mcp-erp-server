using ErpMcp.Application.Common;
using ErpMcp.Application.Customers;
using ErpMcp.Domain.Customers;

namespace ErpMcp.IntegrationTests.Repositories;

public class CustomerQueriesTests(PostgresFixture db)
{
    private readonly CustomerQueries _customers = db.GetService<CustomerQueries>();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_matches_name_case_insensitively()
    {
        var result = await _customers.SearchAsync("bistro", null, null, null, Ct);

        result.Items.ShouldHaveSingleItem().Code.ShouldBe("CUST-0001");
    }

    [Fact]
    public async Task Search_matches_city()
    {
        var result = await _customers.SearchAsync("leeds", null, null, null, Ct);

        result.Items.ShouldAllBe(c => c.City == "Leeds");
        result.Items.Select(c => c.Code).ShouldBe(["CUST-0004", "CUST-0011"], ignoreOrder: true);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var result = await _customers.SearchAsync("%", null, null, null, Ct);

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Search_filters_by_status()
    {
        var result = await _customers.SearchAsync(null, "on_hold", null, null, Ct);

        result.Items.Select(c => c.Code).ShouldBe(["CUST-0006", "CUST-0012"], ignoreOrder: true);
        result.Items.ShouldAllBe(c => c.Status == AccountStatus.OnHold && !c.CanPlaceOrders);
    }

    [Fact]
    public async Task Search_pages_through_results()
    {
        var first = await _customers.SearchAsync(null, null, 15, 0, Ct);
        var second = await _customers.SearchAsync(null, null, 15, 15, Ct);

        first.TotalCount.ShouldBe(20);
        first.HasMore.ShouldBeTrue();
        second.Items.Count.ShouldBe(5);
        second.HasMore.ShouldBeFalse();
        first.Items.Select(c => c.Code).Intersect(second.Items.Select(c => c.Code)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_returns_customer_with_order_stats()
    {
        var details = await _customers.GetAsync("cust-0015", Ct);

        details.Customer.Name.ShouldBe("Pennine Events Catering");
        details.Orders.TotalOrders.ShouldBeGreaterThan(0);
        details.Orders.LastOrderAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_unknown_customer_throws_not_found() =>
        await Should.ThrowAsync<NotFoundException>(() => _customers.GetAsync("CUST-9999", Ct));
}
