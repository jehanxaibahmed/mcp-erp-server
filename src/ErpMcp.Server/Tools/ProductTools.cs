using System.ComponentModel;
using ErpMcp.Application.Catalog;
using ErpMcp.Application.Common;
using ErpMcp.Application.Security;
using ErpMcp.Domain.Catalog;
using ErpMcp.Server.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class ProductTools(ProductQueries products)
{
    [RequiresScope(Scopes.CatalogRead)]
    [McpServerTool(Name = "search_products", Title = "Search products", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search the product catalogue by name or SKU, optionally within one category. Prices are in GBP, excluding VAT.")]
    public Task<PagedResult<Product>> SearchProducts(
        [Description("Text to match against product name or SKU (case-insensitive). Omit to list all.")] string? query = null,
        [Description("Exact category name, e.g. 'Dairy'. Use list_product_categories to see options.")] string? category = null,
        [Description("Include discontinued products. Default false.")] bool includeInactive = false,
        [Description("Page size, 1-100. Default 20.")] int? limit = null,
        [Description("Number of results to skip, for paging.")] int? offset = null,
        CancellationToken ct = default) =>
        products.SearchAsync(query, category, includeInactive, limit, offset, ct);

    [RequiresScope(Scopes.CatalogRead)]
    [McpServerTool(Name = "get_product", Title = "Get product", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get one product by SKU, including its price and whether it is still sold.")]
    public Task<Product> GetProduct(
        [Description("Product SKU, e.g. BEV-0001.")] string sku,
        CancellationToken ct = default) =>
        products.GetAsync(sku, ct);

    [RequiresScope(Scopes.CatalogRead)]
    [McpServerTool(Name = "list_product_categories", Title = "List product categories", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List catalogue categories with the number of active products in each.")]
    public async Task<CategoryList> ListProductCategories(CancellationToken ct = default) =>
        new(await products.ListCategoriesAsync(ct));

    /// <summary>Wrapped in an object so the structured output schema has a named property, not a bare array.</summary>
    public sealed record CategoryList(IReadOnlyList<ProductCategory> Categories);
}
