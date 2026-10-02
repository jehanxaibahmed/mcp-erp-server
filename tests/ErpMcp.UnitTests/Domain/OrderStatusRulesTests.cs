using ErpMcp.Domain.Common;
using ErpMcp.Domain.Orders;

namespace ErpMcp.UnitTests.Domain;

public class OrderStatusRulesTests
{
    [Theory]
    [InlineData(OrderStatus.PendingApproval, OrderStatus.Approved)]
    [InlineData(OrderStatus.PendingApproval, OrderStatus.Rejected)]
    [InlineData(OrderStatus.PendingApproval, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Approved, OrderStatus.Fulfilled)]
    [InlineData(OrderStatus.Approved, OrderStatus.Cancelled)]
    public void Allows_lifecycle_transitions(OrderStatus from, OrderStatus to) =>
        OrderStatusRules.CanTransition(from, to).ShouldBeTrue();

    [Theory]
    [InlineData(OrderStatus.PendingApproval, OrderStatus.Fulfilled)]
    [InlineData(OrderStatus.Approved, OrderStatus.Rejected)]
    [InlineData(OrderStatus.Rejected, OrderStatus.Approved)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.PendingApproval)]
    public void Blocks_everything_else(OrderStatus from, OrderStatus to)
    {
        OrderStatusRules.CanTransition(from, to).ShouldBeFalse();
        Should.Throw<DomainRuleViolationException>(() => OrderStatusRules.EnsureCanTransition("SO-1", from, to));
    }

    [Fact]
    public void Terminal_states_have_no_exits()
    {
        foreach (var terminal in new[] { OrderStatus.Rejected, OrderStatus.Fulfilled, OrderStatus.Cancelled })
        {
            Enum.GetValues<OrderStatus>().ShouldAllBe(to => !OrderStatusRules.CanTransition(terminal, to));
        }
    }
}
