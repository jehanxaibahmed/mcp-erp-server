using ErpMcp.Application.Common;
using ErpMcp.Domain.Customers;

namespace ErpMcp.Application.Customers;

public sealed class CustomerQueries(ICustomerRepository customers)
{
    public Task<PagedResult<Customer>> SearchAsync(
        string? query, string? status, int? limit, int? offset, CancellationToken ct) =>
        customers.SearchAsync(
            Guard.SearchText(query),
            Guard.OptionalEnum<AccountStatus>(status, "status"),
            PageRequest.Create(limit, offset),
            ct);

    public async Task<CustomerDetails> GetAsync(string? customerCode, CancellationToken ct)
    {
        var code = Guard.CustomerCode(customerCode);
        var customer = await customers.GetByCodeAsync(code, ct)
            ?? throw new NotFoundException("Customer", code);
        var stats = await customers.GetOrderStatsAsync(customer.Id, ct);
        return new CustomerDetails(customer, stats);
    }
}

public sealed record CustomerDetails(Customer Customer, CustomerOrderStats Orders);
