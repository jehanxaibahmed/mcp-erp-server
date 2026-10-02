using ErpMcp.Domain.Common;
using ErpMcp.Domain.Inventory;

namespace ErpMcp.UnitTests.Domain;

public class StockAllocatorTests
{
    private static StockAvailability Stock(string sku, string warehouse, int available) =>
        new(sku.GetHashCode(StringComparison.Ordinal), sku, warehouse.GetHashCode(StringComparison.Ordinal), warehouse, available);

    private static IReadOnlyList<StockAllocation> Allocate(AllocationRequest[] lines, params StockAvailability[] stock) =>
        StockAllocator.Allocate("SO-100001", lines, stock);

    [Fact]
    public void Uses_one_warehouse_when_one_can_cover_the_line_preferring_the_best_stocked()
    {
        var result = Allocate([new(1, "BEV-0001", 30)],
            Stock("BEV-0001", "WH-MAN", 40), Stock("BEV-0001", "WH-BHM", 90), Stock("BEV-0001", "WH-LDS", 10));

        result.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            a => a.WarehouseCode.ShouldBe("WH-BHM"),
            a => a.Quantity.ShouldBe(30));
    }

    [Fact]
    public void Splits_across_warehouses_largest_first_when_no_single_one_suffices()
    {
        var result = Allocate([new(1, "BEV-0001", 100)],
            Stock("BEV-0001", "WH-MAN", 40), Stock("BEV-0001", "WH-BHM", 50), Stock("BEV-0001", "WH-LDS", 30));

        result.Select(a => (a.WarehouseCode, a.Quantity)).ShouldBe([("WH-BHM", 50), ("WH-MAN", 40), ("WH-LDS", 10)]);
    }

    [Fact]
    public void Lines_for_the_same_sku_share_one_pool()
    {
        var result = Allocate([new(1, "BEV-0001", 30), new(2, "BEV-0001", 30)],
            Stock("BEV-0001", "WH-MAN", 40), Stock("BEV-0001", "WH-BHM", 25));

        result.Sum(a => a.Quantity).ShouldBe(60);
        result.Where(a => a.WarehouseCode == "WH-MAN").Sum(a => a.Quantity).ShouldBeLessThanOrEqualTo(40);
        result.Where(a => a.WarehouseCode == "WH-BHM").Sum(a => a.Quantity).ShouldBeLessThanOrEqualTo(25);
    }

    [Fact]
    public void Refuses_with_the_shortfall_when_total_stock_is_insufficient()
    {
        var ex = Should.Throw<DomainRuleViolationException>(() => Allocate(
            [new(1, "BEV-0001", 100), new(2, "DAI-0003", 1)],
            Stock("BEV-0001", "WH-MAN", 40), Stock("BEV-0001", "WH-BHM", 50), Stock("DAI-0003", "WH-MAN", 5)));

        ex.Message.ShouldBe("Cannot approve SO-100001: insufficient stock (BEV-0001 needs 100, available 90). Reject it or wait for stock to arrive.");
    }

    [Fact]
    public void Product_with_no_stock_rows_is_a_shortfall() =>
        Should.Throw<DomainRuleViolationException>(() => Allocate([new(1, "NEW-0001", 1)]))
            .Message.ShouldContain("NEW-0001 needs 1, available 0");

    [Fact]
    public void Exactly_enough_stock_is_fully_allocated()
    {
        var result = Allocate([new(1, "BEV-0001", 70)], Stock("BEV-0001", "WH-MAN", 40), Stock("BEV-0001", "WH-BHM", 30));

        result.Sum(a => a.Quantity).ShouldBe(70);
    }
}
