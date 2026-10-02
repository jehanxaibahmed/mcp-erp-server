using ErpMcp.Application.Common;
using ErpMcp.Domain.Orders;

namespace ErpMcp.Application.Orders;

public sealed class OrderQueries(IOrderRepository orders)
{
    public Task<PagedResult<OrderSummary>> ListAsync(
        string? customerCode,
        string? status,
        DateOnly? createdFrom,
        DateOnly? createdTo,
        int? limit,
        int? offset,
        CancellationToken ct)
    {
        if (createdFrom > createdTo)
        {
            throw new InputValidationException("createdFrom", "must be on or before 'createdTo'.");
        }

        var filter = new OrderFilter(
            customerCode is null ? null : Guard.CustomerCode(customerCode),
            Guard.OptionalEnum<OrderStatus>(status, "status"),
            createdFrom,
            createdTo);

        return orders.ListAsync(filter, PageRequest.Create(limit, offset), ct);
    }

    public async Task<Order> GetAsync(string? orderNumber, CancellationToken ct)
    {
        var number = Guard.OrderNumber(orderNumber);
        return await orders.GetByNumberAsync(number, ct)
            ?? throw new NotFoundException("Order", number);
    }
}
