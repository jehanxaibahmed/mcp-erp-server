namespace ErpMcp.Domain.Inventory;

/// <summary>Stock of one product in one warehouse.</summary>
public sealed record StockLevel(
    string Sku,
    string ProductName,
    string WarehouseCode,
    string WarehouseName,
    int QuantityOnHand,
    int QuantityReserved,
    int ReorderLevel)
{
    /// <summary>Stock that can still be promised to new orders.</summary>
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    public bool IsBelowReorderLevel => QuantityAvailable < ReorderLevel;
}
