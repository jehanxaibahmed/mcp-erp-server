using ErpMcp.Application.Common;
using ErpMcp.Domain.Inventory;

namespace ErpMcp.Application.Inventory;

public interface IStockRepository
{
    Task<IReadOnlyList<StockLevel>> GetForProductAsync(string sku, CancellationToken ct);

    /// <summary>Total available quantity (on hand minus reserved, all warehouses) per SKU.</summary>
    Task<IReadOnlyDictionary<string, int>> GetAvailableBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct);

    Task<PagedResult<StockLevel>> ListBelowReorderLevelAsync(string? warehouseCode, PageRequest page, CancellationToken ct);
}
