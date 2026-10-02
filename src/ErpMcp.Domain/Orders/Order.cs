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
    IReadOnlyList<OrderLine> Lines);

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
