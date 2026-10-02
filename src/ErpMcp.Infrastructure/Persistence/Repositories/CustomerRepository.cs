using Dapper;
using ErpMcp.Application.Common;
using ErpMcp.Application.Customers;
using ErpMcp.Domain.Customers;
using Npgsql;

namespace ErpMcp.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(NpgsqlDataSource db) : ICustomerRepository
{
    private const string SelectColumns = """
        id AS Id, code AS Code, name AS Name, email AS Email, phone AS Phone, city AS City,
        country_code AS CountryCode, credit_limit AS CreditLimit, account_status AS AccountStatus
        """;

    public async Task<PagedResult<Customer>> SearchAsync(
        string? text, AccountStatus? status, PageRequest page, CancellationToken ct)
    {
        const string where = """
            WHERE (@Pattern::text IS NULL OR name ILIKE @Pattern OR code ILIKE @Pattern OR city ILIKE @Pattern)
              AND (@Status::text IS NULL OR account_status = @Status)
            """;

        var args = new
        {
            Pattern = SqlText.ContainsPattern(text),
            Status = status is null ? null : SnakeCase.From(status.Value),
            page.Limit,
            page.Offset,
        };

        await using var connection = await db.OpenConnectionAsync(ct);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT count(*)::int FROM erp.customers {where};
            SELECT {SelectColumns} FROM erp.customers {where}
            ORDER BY name, id LIMIT @Limit OFFSET @Offset;
            """, args, cancellationToken: ct));

        var total = await results.ReadSingleAsync<int>();
        var rows = await results.ReadAsync<CustomerRow>();
        return new PagedResult<Customer>(rows.Select(r => r.ToDomain()).ToList(), total, page.Limit, page.Offset);
    }

    public async Task<Customer?> GetByCodeAsync(string code, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<CustomerRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM erp.customers WHERE code = @code",
            new { code },
            cancellationToken: ct));
        return row?.ToDomain();
    }

    public async Task<CustomerOrderStats> GetOrderStatsAsync(long customerId, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleAsync<StatsRow>(new CommandDefinition("""
            SELECT count(*)::int                                                                   AS TotalOrders,
                   (count(*) FILTER (WHERE status IN ('pending_approval', 'approved')))::int       AS OpenOrderCount,
                   coalesce(sum(total_amount) FILTER (WHERE status IN ('pending_approval', 'approved')), 0) AS OpenOrderValue,
                   max(created_at)                                                                 AS LastOrderAt
            FROM erp.orders
            WHERE customer_id = @customerId
            """, new { customerId }, cancellationToken: ct));

        return new CustomerOrderStats(row.TotalOrders, row.OpenOrderCount, row.OpenOrderValue, SqlText.AsUtc(row.LastOrderAt));
    }

    private sealed record CustomerRow(
        long Id, string Code, string Name, string Email, string? Phone, string City,
        string CountryCode, decimal CreditLimit, string AccountStatus)
    {
        public Customer ToDomain() => new(
            Id, Code, Name, Email, Phone, City, CountryCode, CreditLimit,
            SnakeCase.To<AccountStatus>(AccountStatus));
    }

    private sealed record StatsRow(int TotalOrders, int OpenOrderCount, decimal OpenOrderValue, DateTime? LastOrderAt);
}
