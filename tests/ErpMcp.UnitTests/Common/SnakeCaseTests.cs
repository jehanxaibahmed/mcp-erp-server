using ErpMcp.Application.Common;
using ErpMcp.Domain.Customers;
using ErpMcp.Domain.Orders;

namespace ErpMcp.UnitTests.Common;

public class SnakeCaseTests
{
    [Fact]
    public void Round_trips_every_order_status()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            SnakeCase.To<OrderStatus>(SnakeCase.From(status)).ShouldBe(status);
        }
    }

    [Fact]
    public void Matches_database_vocabulary()
    {
        SnakeCase.From(OrderStatus.PendingApproval).ShouldBe("pending_approval");
        SnakeCase.From(AccountStatus.OnHold).ShouldBe("on_hold");
    }
}
