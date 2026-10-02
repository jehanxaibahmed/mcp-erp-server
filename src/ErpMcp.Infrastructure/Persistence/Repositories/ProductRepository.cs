using Dapper;
using ErpMcp.Application.Catalog;
using ErpMcp.Application.Common;
using ErpMcp.Domain.Catalog;
using Npgsql;

namespace ErpMcp.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(NpgsqlDataSource db) : IProductRepository
{
    private const string SelectColumns = """
        id AS Id, sku AS Sku, name AS Name, category AS Category,
        unit_of_measure AS UnitOfMeasure, unit_price AS UnitPrice, is_active AS IsActive
        """;

    public async Task<PagedResult<Product>> SearchAsync(
        string? text, string? category, bool includeInactive, PageRequest page, CancellationToken ct)
    {
        const string where = """
            WHERE (@Pattern::text IS NULL OR name ILIKE @Pattern OR sku ILIKE @Pattern)
              AND (@Category::text IS NULL OR lower(category) = lower(@Category))
              AND (@IncludeInactive OR is_active)
            """;

        var args = new
        {
            Pattern = SqlText.ContainsPattern(text),
            Category = category,
            IncludeInactive = includeInactive,
            page.Limit,
            page.Offset,
        };

        await using var connection = await db.OpenConnectionAsync(ct);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT count(*)::int FROM erp.products {where};
            SELECT {SelectColumns} FROM erp.products {where}
            ORDER BY category, name LIMIT @Limit OFFSET @Offset;
            """, args, cancellationToken: ct));

        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<Product>()).ToList();
        return new PagedResult<Product>(items, total, page.Limit, page.Offset);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM erp.products WHERE sku = @sku",
            new { sku },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Product>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var products = await connection.QueryAsync<Product>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM erp.products WHERE sku = ANY(@skus)",
            new { skus = skus.ToArray() },
            cancellationToken: ct));
        return products.ToList();
    }

    public async Task<IReadOnlyList<ProductCategory>> ListCategoriesAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var categories = await connection.QueryAsync<ProductCategory>(new CommandDefinition("""
            SELECT category AS Name, (count(*) FILTER (WHERE is_active))::int AS ActiveProductCount
            FROM erp.products
            GROUP BY category
            ORDER BY category
            """, cancellationToken: ct));
        return categories.ToList();
    }
}
