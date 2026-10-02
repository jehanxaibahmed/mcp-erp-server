using System.ComponentModel;
using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Application.Security;
using ErpMcp.Domain.Orders;
using ErpMcp.Server.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class OrderTools(OrderQueries orders)
{
    [RequiresScope(Scopes.OrdersRead)]
    [McpServerTool(Name = "list_orders", Title = "List orders", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List sales orders, newest first, filtered by customer, status and creation date.")]
    public Task<PagedResult<OrderSummary>> ListOrders(
        [Description("Only orders for this customer code, e.g. CUST-0001.")] string? customerCode = null,
        [Description("Filter by status: pending_approval, approved, rejected, fulfilled or cancelled.")] string? status = null,
        [Description("Earliest creation date (inclusive), YYYY-MM-DD, UTC.")] DateOnly? createdFrom = null,
        [Description("Latest creation date (inclusive), YYYY-MM-DD, UTC.")] DateOnly? createdTo = null,
        [Description("Page size, 1-100. Default 20.")] int? limit = null,
        [Description("Number of results to skip, for paging.")] int? offset = null,
        CancellationToken ct = default) =>
        orders.ListAsync(customerCode, status, createdFrom, createdTo, limit, offset, ct);

    [RequiresScope(Scopes.OrdersRead)]
    [McpServerTool(Name = "get_order", Title = "Get order", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Get one order with its lines, totals and approval decision.")]
    public Task<Order> GetOrder(
        [Description("Order number, e.g. SO-100001.")] string orderNumber,
        CancellationToken ct = default) =>
        orders.GetAsync(orderNumber, ct);
}
