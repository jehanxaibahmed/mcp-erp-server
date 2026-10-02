using ErpMcp.Application.Catalog;
using ErpMcp.Application.Common;
using ErpMcp.Application.Inventory;

namespace ErpMcp.IntegrationTests.Repositories;

public class CatalogAndStockQueriesTests(PostgresFixture db)
{
    private readonly ProductQueries _products = db.GetService<ProductQueries>();
    private readonly StockQueries _stock = db.GetService<StockQueries>();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_hides_inactive_products_by_default()
    {
        var active = await _products.SearchAsync("cutlery", null, includeInactive: false, null, null, Ct);
        var all = await _products.SearchAsync("cutlery", null, includeInactive: true, null, null, Ct);

        active.TotalCount.ShouldBe(0);
        all.Items.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Search_filters_by_category_ignoring_case()
    {
        var result = await _products.SearchAsync(null, "dairy", false, null, null, Ct);

        result.TotalCount.ShouldBe(5);
        result.Items.ShouldAllBe(p => p.Category == "Dairy");
    }

    [Fact]
    public async Task Categories_count_only_active_products()
    {
        var categories = await _products.ListCategoriesAsync(Ct);

        categories.Single(c => c.Name == "Packaging").ActiveProductCount.ShouldBe(2);
        categories.Sum(c => c.ActiveProductCount).ShouldBe(29);
    }

    [Fact]
    public async Task Stock_for_product_covers_every_warehouse()
    {
        var stock = await _stock.GetForProductAsync("BEV-0001", Ct);

        stock.Warehouses.Select(w => w.WarehouseCode).ShouldBe(["WH-BHM", "WH-LDS", "WH-MAN"]);
        stock.TotalOnHand.ShouldBe(stock.Warehouses.Sum(w => w.QuantityOnHand));
        stock.TotalAvailable.ShouldBeLessThanOrEqualTo(stock.TotalOnHand);
    }

    [Fact]
    public async Task Stock_for_unknown_product_throws_not_found() =>
        await Should.ThrowAsync<NotFoundException>(() => _stock.GetForProductAsync("ZZZ-9999", Ct));

    [Fact]
    public async Task Low_stock_lists_only_rows_below_reorder_level_most_urgent_first()
    {
        var result = await _stock.ListLowStockAsync(null, 100, null, Ct);

        result.TotalCount.ShouldBe(11);
        result.Items.ShouldAllBe(s => s.IsBelowReorderLevel);
        var shortfalls = result.Items.Select(s => s.QuantityAvailable - s.ReorderLevel).ToList();
        shortfalls.ShouldBe(shortfalls.Order().ToList());
    }

    [Fact]
    public async Task Low_stock_can_be_limited_to_one_warehouse()
    {
        var result = await _stock.ListLowStockAsync("wh-man", null, null, Ct);

        result.Items.ShouldAllBe(s => s.WarehouseCode == "WH-MAN");
    }
}
