using ErpMcp.Application.Common;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Orders;

namespace ErpMcp.Application.Orders;

/// <summary>
/// The end of the order lifecycle: shipping approved orders and cancelling orders that will not
/// ship. Operator-only, like approval.
/// </summary>
public sealed class OrderFulfilmentService(IOrderRepository orders, IOrderWriter writer, TimeProvider clock)
{
    public const int MaxReasonLength = 500;

    /// <summary>Marks an approved order as shipped and consumes its reserved stock.</summary>
    public Task<Order> FulfilAsync(string? orderNumber, string? operatorId, CancellationToken ct) =>
        CloseAsync(orderNumber, operatorId, OrderStatus.Fulfilled, reason: null, ct);

    /// <summary>Cancels a pending or approved order, releasing any reserved stock.</summary>
    public Task<Order> CancelAsync(string? orderNumber, string? operatorId, string? reason, CancellationToken ct) =>
        CloseAsync(orderNumber, operatorId, OrderStatus.Cancelled, Guard.Reason(reason, "reason", MaxReasonLength), ct);

    private async Task<Order> CloseAsync(
        string? orderNumber, string? operatorId, OrderStatus closeAs, string? reason, CancellationToken ct)
    {
        var number = Guard.OrderNumber(orderNumber);
        var closedBy = Guard.PersonActor(operatorId, "operator");

        var order = await orders.GetByNumberAsync(number, ct) ?? throw new NotFoundException("Order", number);
        OrderStatusRules.EnsureCanTransition(order.OrderNumber, order.Status, closeAs);

        if (!await writer.TryCloseAsync(order.Id, order.Status, closeAs, closedBy, reason, clock.GetUtcNow(), ct))
        {
            throw new DomainRuleViolationException(
                $"Order {order.OrderNumber} changed while you were working on it. Reload it and check.");
        }

        return await orders.GetByNumberAsync(number, ct) ?? throw new NotFoundException("Order", number);
    }
}
