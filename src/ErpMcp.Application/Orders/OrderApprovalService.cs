using ErpMcp.Application.Common;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Inventory;
using ErpMcp.Domain.Orders;

namespace ErpMcp.Application.Orders;

/// <summary>
/// Human approval decisions. Deliberately not exposed as an MCP tool: it is reachable only from
/// the operator CLI, so an agent can never approve the orders it drafts.
/// </summary>
public sealed class OrderApprovalService(IOrderRepository orders, IOrderWriter writer, TimeProvider clock)
{
    public const int MaxReasonLength = 500;

    public Task<PagedResult<OrderSummary>> ListPendingAsync(int? limit, CancellationToken ct) =>
        orders.ListAsync(new OrderFilter(null, OrderStatus.PendingApproval, null, null), PageRequest.Create(limit), ct);

    /// <summary>Approves the order and reserves its stock across warehouses.</summary>
    public Task<Order> ApproveAsync(string? orderNumber, string? approver, CancellationToken ct) =>
        DecideAsync(orderNumber, approver, OrderStatus.Approved, (order, decidedBy, at) =>
            writer.TryApproveAndReserveAsync(order.Id, decidedBy, at, stock => StockAllocator.Allocate(
                order.OrderNumber,
                order.Lines.Select(l => new AllocationRequest(l.LineNumber, l.Sku, l.Quantity)).ToList(),
                stock), ct), ct);

    public Task<Order> RejectAsync(string? orderNumber, string? approver, string? reason, CancellationToken ct)
    {
        var why = Guard.Reason(reason, "reason", MaxReasonLength);
        return DecideAsync(orderNumber, approver, OrderStatus.Rejected, (order, decidedBy, at) =>
            writer.TryRejectAsync(order.Id, decidedBy, why, at, ct), ct);
    }

    private async Task<Order> DecideAsync(
        string? orderNumber,
        string? approver,
        OrderStatus decision,
        Func<Order, string, DateTimeOffset, Task<bool>> record,
        CancellationToken ct)
    {
        var number = Guard.OrderNumber(orderNumber);
        var decidedBy = Guard.PersonActor(approver, "approver");

        var order = await orders.GetByNumberAsync(number, ct) ?? throw new NotFoundException("Order", number);
        OrderStatusRules.EnsureCanTransition(order.OrderNumber, order.Status, decision);

        if (string.Equals(order.CreatedBy, decidedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleViolationException(
                $"{decidedBy} created order {order.OrderNumber} and cannot also decide on it.");
        }

        if (!await record(order, decidedBy, clock.GetUtcNow()))
        {
            throw new DomainRuleViolationException(
                $"Order {order.OrderNumber} was decided by someone else in the meantime. Reload it and check.");
        }

        return await orders.GetByNumberAsync(number, ct) ?? throw new NotFoundException("Order", number);
    }
}
