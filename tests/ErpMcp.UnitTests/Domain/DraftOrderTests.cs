using ErpMcp.Domain.Catalog;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Customers;
using ErpMcp.Domain.Orders;

namespace ErpMcp.UnitTests.Domain;

public class DraftOrderTests
{
    private static readonly Customer ActiveCustomer =
        new(1, "CUST-0001", "Test Bistro", "a@b.example", null, "Leeds", "GB", 1_000m, AccountStatus.Active);

    private static readonly Product Coffee = new(10, "BEV-0001", "Coffee 1kg", "Beverages", "pack", 14.50m, true);
    private static readonly Product Butter = new(11, "DAI-0003", "Butter x40", "Dairy", "case", 58.00m, true);
    private static readonly Product Cutlery = new(12, "PKG-0003", "Wooden cutlery", "Packaging", "case", 19.95m, false);

    private static readonly Dictionary<string, Product> Catalogue = new[] { Coffee, Butter, Cutlery }.ToDictionary(p => p.Sku);
    private static readonly Dictionary<string, int> PlentyOfStock = new() { ["BEV-0001"] = 500, ["DAI-0003"] = 500, ["PKG-0003"] = 500 };

    private static DraftOrder Create(
        (string, int)[] lines,
        Customer? customer = null,
        Dictionary<string, int>? stock = null,
        decimal openOrderValue = 0,
        string? notes = null) =>
        DraftOrder.Create(customer ?? ActiveCustomer, lines, Catalogue, stock ?? PlentyOfStock, openOrderValue, notes);

    [Fact]
    public void Prices_lines_from_the_catalogue()
    {
        var draft = Create([("BEV-0001", 4), ("DAI-0003", 1)]);

        draft.Lines.Select(l => (l.LineNumber, l.Product.Sku, l.UnitPrice, l.LineTotal)).ShouldBe(
        [
            (1, "BEV-0001", 14.50m, 58.00m),
            (2, "DAI-0003", 58.00m, 58.00m),
        ]);
        draft.TotalAmount.ShouldBe(116.00m);
        draft.ReviewFlags.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AccountStatus.OnHold)]
    [InlineData(AccountStatus.Closed)]
    public void Rejects_customers_that_cannot_order(AccountStatus status)
    {
        var customer = ActiveCustomer with { Status = status };

        var ex = Should.Throw<DomainRuleViolationException>(() => Create([("BEV-0001", 1)], customer));
        ex.Message.ShouldContain("cannot place orders");
    }

    [Fact]
    public void Rejects_empty_orders() =>
        Should.Throw<DomainRuleViolationException>(() => Create([]));

    [Fact]
    public void Rejects_too_many_lines()
    {
        var lines = Enumerable.Range(0, DraftOrder.MaxLines + 1).Select(_ => ("BEV-0001", 1)).ToArray();
        Should.Throw<DomainRuleViolationException>(() => Create(lines)).Message.ShouldContain("at most");
    }

    [Fact]
    public void Rejects_duplicate_skus() =>
        Should.Throw<DomainRuleViolationException>(() => Create([("BEV-0001", 1), ("BEV-0001", 2)]))
            .Message.ShouldContain("more than once");

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(DraftOrder.MaxQuantityPerLine + 1)]
    public void Rejects_out_of_range_quantities(int quantity) =>
        Should.Throw<DomainRuleViolationException>(() => Create([("BEV-0001", quantity)]));

    [Fact]
    public void Rejects_unknown_products() =>
        Should.Throw<DomainRuleViolationException>(() => Create([("ZZZ-0001", 1)])).Message.ShouldContain("does not exist");

    [Fact]
    public void Rejects_discontinued_products() =>
        Should.Throw<DomainRuleViolationException>(() => Create([("PKG-0003", 1)])).Message.ShouldContain("discontinued");

    [Fact]
    public void Rejects_overlong_notes() =>
        Should.Throw<DomainRuleViolationException>(() => Create([("BEV-0001", 1)], notes: new string('n', DraftOrder.MaxNotesLength + 1)));

    [Fact]
    public void Flags_stock_shortfall_without_blocking()
    {
        var draft = Create([("BEV-0001", 30)], stock: new() { ["BEV-0001"] = 12 });

        draft.ReviewFlags.ShouldHaveSingleItem().ShouldBe("Insufficient stock for BEV-0001: requested 30, available 12.");
    }

    [Fact]
    public void Flags_credit_limit_including_open_orders()
    {
        // 600 open + 58 * 8 = 464 → 1,064 > 1,000 limit
        var draft = Create([("DAI-0003", 8)], openOrderValue: 600m);

        draft.ReviewFlags.ShouldHaveSingleItem().ShouldBe(
            "Exceeds credit limit: open orders £600.00 + this order £464.00 > limit £1,000.00.");
    }

    [Fact]
    public void Exactly_at_credit_limit_is_not_flagged() =>
        Create([("DAI-0003", 10)], openOrderValue: 420m).ReviewFlags.ShouldBeEmpty();

    [Fact]
    public void Blank_notes_are_dropped() =>
        Create([("BEV-0001", 1)], notes: "   ").Notes.ShouldBeNull();
}
