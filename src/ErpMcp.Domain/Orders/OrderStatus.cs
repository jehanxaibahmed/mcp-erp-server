namespace ErpMcp.Domain.Orders;

public enum OrderStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Fulfilled,
    Cancelled,
}
