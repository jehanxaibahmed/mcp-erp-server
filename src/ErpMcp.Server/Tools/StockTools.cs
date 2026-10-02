using System.ComponentModel;
using ErpMcp.Application.Common;
using ErpMcp.Application.Inventory;
using ErpMcp.Domain.Inventory;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class StockTools(StockQueries stock)
{
    [McpServerTool(Name = "get_stock_level", Title = "Get stock level", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Get current stock for a product in every warehouse. 'Available' is on-hand minus stock already reserved for orders.")]
    public Task<ProductStock> GetStockLevel(
        [Description("Product SKU, e.g. BEV-0001.")] string sku,
        CancellationToken ct = default) =>
        stock.GetForProductAsync(sku, ct);

    [McpServerTool(Name = "list_low_stock", Title = "List low stock", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List active products whose available stock is below the reorder level, most urgent first.")]
    public Task<PagedResult<StockLevel>> ListLowStock(
        [Description("Limit to one warehouse: WH-MAN, WH-BHM or WH-LDS. Omit for all.")] string? warehouseCode = null,
        [Description("Page size, 1-100. Default 20.")] int? limit = null,
        [Description("Number of results to skip, for paging.")] int? offset = null,
        CancellationToken ct = default) =>
        stock.ListLowStockAsync(warehouseCode, limit, offset, ct);
}
