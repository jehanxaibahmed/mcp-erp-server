using ErpMcp.Application.Catalog;
using ErpMcp.Application.Common;
using ErpMcp.Domain.Inventory;

namespace ErpMcp.Application.Inventory;

public sealed class StockQueries(IStockRepository stock, IProductRepository products)
{
    public async Task<ProductStock> GetForProductAsync(string? sku, CancellationToken ct)
    {
        var normalised = Guard.Sku(sku);
        var product = await products.GetBySkuAsync(normalised, ct)
            ?? throw new NotFoundException("Product", normalised);

        var levels = await stock.GetForProductAsync(normalised, ct);
        return new ProductStock(
            product.Sku,
            product.Name,
            product.IsActive,
            levels.Sum(l => l.QuantityOnHand),
            levels.Sum(l => l.QuantityAvailable),
            levels);
    }

    public Task<PagedResult<StockLevel>> ListLowStockAsync(
        string? warehouseCode, int? limit, int? offset, CancellationToken ct) =>
        stock.ListBelowReorderLevelAsync(
            warehouseCode is null ? null : Guard.WarehouseCode(warehouseCode),
            PageRequest.Create(limit, offset),
            ct);
}

public sealed record ProductStock(
    string Sku,
    string ProductName,
    bool IsActive,
    int TotalOnHand,
    int TotalAvailable,
    IReadOnlyList<StockLevel> Warehouses);
