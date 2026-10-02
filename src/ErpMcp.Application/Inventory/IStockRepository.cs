using ErpMcp.Application.Common;
using ErpMcp.Domain.Inventory;

namespace ErpMcp.Application.Inventory;

public interface IStockRepository
{
    Task<IReadOnlyList<StockLevel>> GetForProductAsync(string sku, CancellationToken ct);

    Task<PagedResult<StockLevel>> ListBelowReorderLevelAsync(string? warehouseCode, PageRequest page, CancellationToken ct);
}
