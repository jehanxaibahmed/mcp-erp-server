using ErpMcp.Application.Common;
using ErpMcp.Domain.Catalog;

namespace ErpMcp.Application.Catalog;

public interface IProductRepository
{
    Task<PagedResult<Product>> SearchAsync(string? text, string? category, bool includeInactive, PageRequest page, CancellationToken ct);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct);

    Task<IReadOnlyList<Product>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct);

    Task<IReadOnlyList<ProductCategory>> ListCategoriesAsync(CancellationToken ct);
}

public sealed record ProductCategory(string Name, int ActiveProductCount);
