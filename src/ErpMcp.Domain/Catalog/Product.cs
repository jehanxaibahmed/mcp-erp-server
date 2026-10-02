namespace ErpMcp.Domain.Catalog;

public sealed record Product(
    long Id,
    string Sku,
    string Name,
    string Category,
    string UnitOfMeasure,
    decimal UnitPrice,
    bool IsActive);
