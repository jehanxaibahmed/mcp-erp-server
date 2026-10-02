using Dapper;
using ErpMcp.Application.Common;
using ErpMcp.Application.Inventory;
using ErpMcp.Domain.Inventory;
using Npgsql;

namespace ErpMcp.Infrastructure.Persistence.Repositories;

internal sealed class StockRepository(NpgsqlDataSource db) : IStockRepository
{
    private const string FromClause = """
        FROM erp.stock_levels s
        JOIN erp.products p ON p.id = s.product_id
        JOIN erp.warehouses w ON w.id = s.warehouse_id
        """;

    private const string SelectColumns = """
        p.sku AS Sku, p.name AS ProductName, w.code AS WarehouseCode, w.name AS WarehouseName,
        s.quantity_on_hand AS QuantityOnHand, s.quantity_reserved AS QuantityReserved, s.reorder_level AS ReorderLevel
        """;

    public async Task<IReadOnlyList<StockLevel>> GetForProductAsync(string sku, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var levels = await connection.QueryAsync<StockLevel>(new CommandDefinition(
            $"SELECT {SelectColumns} {FromClause} WHERE p.sku = @sku ORDER BY w.code",
            new { sku },
            cancellationToken: ct));
        return levels.ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetAvailableBySkusAsync(
        IReadOnlyCollection<string> skus, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<(string Sku, int Available)>(new CommandDefinition("""
            SELECT p.sku, coalesce(sum(s.quantity_on_hand - s.quantity_reserved), 0)::int
            FROM erp.products p
            LEFT JOIN erp.stock_levels s ON s.product_id = p.id
            WHERE p.sku = ANY(@skus)
            GROUP BY p.sku
            """, new { skus = skus.ToArray() }, cancellationToken: ct));
        return rows.ToDictionary(r => r.Sku, r => r.Available);
    }

    public async Task<PagedResult<StockLevel>> ListBelowReorderLevelAsync(
        string? warehouseCode, PageRequest page, CancellationToken ct)
    {
        const string where = """
            WHERE p.is_active
              AND s.quantity_on_hand - s.quantity_reserved < s.reorder_level
              AND (@WarehouseCode::text IS NULL OR w.code = @WarehouseCode)
            """;

        var args = new { WarehouseCode = warehouseCode, page.Limit, page.Offset };

        await using var connection = await db.OpenConnectionAsync(ct);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT count(*)::int {FromClause} {where};
            SELECT {SelectColumns} {FromClause} {where}
            ORDER BY (s.quantity_on_hand - s.quantity_reserved) - s.reorder_level, p.sku, w.code
            LIMIT @Limit OFFSET @Offset;
            """, args, cancellationToken: ct));

        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<StockLevel>()).ToList();
        return new PagedResult<StockLevel>(items, total, page.Limit, page.Offset);
    }
}
