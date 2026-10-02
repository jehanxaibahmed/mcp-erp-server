using ErpMcp.Application.Common;
using ErpMcp.Domain.Customers;

namespace ErpMcp.Application.Customers;

public interface ICustomerRepository
{
    Task<PagedResult<Customer>> SearchAsync(string? text, AccountStatus? status, PageRequest page, CancellationToken ct);

    Task<Customer?> GetByCodeAsync(string code, CancellationToken ct);

    Task<CustomerOrderStats> GetOrderStatsAsync(long customerId, CancellationToken ct);
}

/// <summary>Order history for a customer. "Open" means pending approval or approved but not yet fulfilled.</summary>
public sealed record CustomerOrderStats(int TotalOrders, int OpenOrderCount, decimal OpenOrderValue, DateTimeOffset? LastOrderAt);
