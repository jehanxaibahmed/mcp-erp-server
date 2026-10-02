using ErpMcp.Domain.Common;

namespace ErpMcp.Domain.Orders;

/// <summary>
/// The order lifecycle. Agents can only create orders in <see cref="OrderStatus.PendingApproval"/>;
/// every later transition is a human decision.
/// </summary>
/// <remarks>
/// pending_approval ─┬─▶ approved ─┬─▶ fulfilled
///                   │             └─▶ cancelled
///                   ├─▶ rejected
///                   └─▶ cancelled
/// </remarks>
public static class OrderStatusRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.PendingApproval] = [OrderStatus.Approved, OrderStatus.Rejected, OrderStatus.Cancelled],
        [OrderStatus.Approved] = [OrderStatus.Fulfilled, OrderStatus.Cancelled],
        [OrderStatus.Rejected] = [],
        [OrderStatus.Fulfilled] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    public static void EnsureCanTransition(string orderNumber, OrderStatus from, OrderStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new DomainRuleViolationException(
                $"Order {orderNumber} is {Describe(from)} and cannot be moved to {Describe(to)}.");
        }
    }

    private static string Describe(OrderStatus status) => status switch
    {
        OrderStatus.PendingApproval => "pending approval",
        _ => status.ToString().ToLowerInvariant(),
    };
}
