using ErpMcp.Application.Common;
using ErpMcp.Domain.Orders;

namespace ErpMcp.Application.Orders;

public interface IOrderRepository
{
    Task<PagedResult<OrderSummary>> ListAsync(OrderFilter filter, PageRequest page, CancellationToken ct);

    Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct);
}

/// <summary>Order list filter. Date bounds are inclusive calendar days (UTC).</summary>
public sealed record OrderFilter(
    string? CustomerCode,
    OrderStatus? Status,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo);
