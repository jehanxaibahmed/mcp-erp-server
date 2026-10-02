namespace ErpMcp.Domain.Orders;

public sealed record Order(
    long Id,
    string OrderNumber,
    string CustomerCode,
    string CustomerName,
    OrderStatus Status,
    OrderSource Source,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? RejectionReason,
    string? Notes,
    decimal TotalAmount,
    IReadOnlyList<string> ReviewFlags,
    IReadOnlyList<OrderLine> Lines)
{
    /// <summary>The authenticated person an agent drafted this order for (HTTP transport), if any.</summary>
    public string? OnBehalfOf { get; init; }

    /// <summary>Who fulfilled or cancelled the order, once it has left the active states.</summary>
    public string? ClosedBy { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>Warehouse reservations made when the order was approved.</summary>
    public IReadOnlyList<OrderAllocation> Allocations { get; init; } = [];
}

public sealed record OrderAllocation(int LineNumber, string Sku, string WarehouseCode, int Quantity);

public sealed record OrderLine(
    int LineNumber,
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>Header-only view of an order for list results.</summary>
public sealed record OrderSummary(
    string OrderNumber,
    string CustomerCode,
    string CustomerName,
    OrderStatus Status,
    OrderSource Source,
    DateTimeOffset CreatedAt,
    decimal TotalAmount,
    int LineCount);
