using ErpMcp.Application.Common;
using ErpMcp.Domain.Common;
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

    public Task<Order> ApproveAsync(string? orderNumber, string? approver, CancellationToken ct) =>
        DecideAsync(orderNumber, approver, OrderStatus.Approved, reason: null, ct);

    public Task<Order> RejectAsync(string? orderNumber, string? approver, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InputValidationException("reason", "is required when rejecting an order.");
        }

        if (reason.Length > MaxReasonLength)
        {
            throw new InputValidationException("reason", $"must be at most {MaxReasonLength} characters.");
        }

        return DecideAsync(orderNumber, approver, OrderStatus.Rejected, reason.Trim(), ct);
    }

    private async Task<Order> DecideAsync(
        string? orderNumber, string? approver, OrderStatus decision, string? reason, CancellationToken ct)
    {
        var number = Guard.OrderNumber(orderNumber);
        var decidedBy = Guard.Actor(approver, "approver");
        if (decidedBy.StartsWith(AgentActor.Prefix, StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException("Orders must be approved or rejected by a person, not an agent identity.");
        }

        var order = await orders.GetByNumberAsync(number, ct) ?? throw new NotFoundException("Order", number);
        OrderStatusRules.EnsureCanTransition(order.OrderNumber, order.Status, decision);

        if (string.Equals(order.CreatedBy, decidedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleViolationException(
                $"{decidedBy} created order {order.OrderNumber} and cannot also decide on it.");
        }

        if (!await writer.TryRecordDecisionAsync(order.Id, decision, decidedBy, reason, clock.GetUtcNow(), ct))
        {
            throw new DomainRuleViolationException(
                $"Order {order.OrderNumber} was decided by someone else in the meantime. Reload it and check.");
        }

        return await orders.GetByNumberAsync(number, ct) ?? throw new NotFoundException("Order", number);
    }
}
