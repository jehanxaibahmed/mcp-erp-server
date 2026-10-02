using ErpMcp.Application.Common;
using ErpMcp.Domain.Catalog;

namespace ErpMcp.Application.Catalog;

public sealed class ProductQueries(IProductRepository products)
{
    public Task<PagedResult<Product>> SearchAsync(
        string? query, string? category, bool includeInactive, int? limit, int? offset, CancellationToken ct) =>
        products.SearchAsync(
            Guard.SearchText(query),
            Guard.SearchText(category, "category"),
            includeInactive,
            PageRequest.Create(limit, offset),
            ct);

    public async Task<Product> GetAsync(string? sku, CancellationToken ct)
    {
        var normalised = Guard.Sku(sku);
        return await products.GetBySkuAsync(normalised, ct)
            ?? throw new NotFoundException("Product", normalised);
    }

    public Task<IReadOnlyList<ProductCategory>> ListCategoriesAsync(CancellationToken ct) =>
        products.ListCategoriesAsync(ct);
}
