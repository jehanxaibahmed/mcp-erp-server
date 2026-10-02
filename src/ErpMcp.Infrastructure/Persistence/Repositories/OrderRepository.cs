using Dapper;
using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Orders;
using Npgsql;

namespace ErpMcp.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(NpgsqlDataSource db) : IOrderRepository
{
    public async Task<PagedResult<OrderSummary>> ListAsync(OrderFilter filter, PageRequest page, CancellationToken ct)
    {
        const string where = """
            WHERE (@CustomerCode::text IS NULL OR c.code = @CustomerCode)
              AND (@Status::text IS NULL OR o.status = @Status)
              AND (@From::timestamptz IS NULL OR o.created_at >= @From)
              AND (@ToExclusive::timestamptz IS NULL OR o.created_at < @ToExclusive)
            """;

        var args = new
        {
            filter.CustomerCode,
            Status = filter.Status is null ? null : SnakeCase.From(filter.Status.Value),
            From = StartOfDayUtc(filter.CreatedFrom),
            ToExclusive = StartOfDayUtc(filter.CreatedTo?.AddDays(1)),
            page.Limit,
            page.Offset,
        };

        await using var connection = await db.OpenConnectionAsync(ct);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT count(*)::int
            FROM erp.orders o JOIN erp.customers c ON c.id = o.customer_id
            {where};

            SELECT o.order_number AS OrderNumber, c.code AS CustomerCode, c.name AS CustomerName,
                   o.status AS Status, o.source AS Source, o.created_at AS CreatedAt, o.total_amount AS TotalAmount,
                   (SELECT count(*)::int FROM erp.order_lines l WHERE l.order_id = o.id) AS LineCount
            FROM erp.orders o JOIN erp.customers c ON c.id = o.customer_id
            {where}
            ORDER BY o.created_at DESC, o.id DESC
            LIMIT @Limit OFFSET @Offset;
            """, args, cancellationToken: ct));

        var total = await results.ReadSingleAsync<int>();
        var rows = await results.ReadAsync<SummaryRow>();
        return new PagedResult<OrderSummary>(rows.Select(r => r.ToDomain()).ToList(), total, page.Limit, page.Offset);
    }

    public async Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT o.id AS Id, o.order_number AS OrderNumber, c.code AS CustomerCode, c.name AS CustomerName,
                   o.status AS Status, o.source AS Source, o.created_by AS CreatedBy, o.created_at AS CreatedAt,
                   o.decided_by AS DecidedBy, o.decided_at AS DecidedAt, o.rejection_reason AS RejectionReason,
                   o.notes AS Notes, o.total_amount AS TotalAmount, o.review_flags AS ReviewFlags
            FROM erp.orders o JOIN erp.customers c ON c.id = o.customer_id
            WHERE o.order_number = @orderNumber;

            SELECT l.line_number AS LineNumber, p.sku AS Sku, p.name AS ProductName,
                   l.quantity AS Quantity, l.unit_price AS UnitPrice, l.line_total AS LineTotal
            FROM erp.order_lines l
            JOIN erp.orders o ON o.id = l.order_id
            JOIN erp.products p ON p.id = l.product_id
            WHERE o.order_number = @orderNumber
            ORDER BY l.line_number;
            """, new { orderNumber }, cancellationToken: ct));

        var header = await results.ReadSingleOrDefaultAsync<OrderRow>();
        if (header is null)
        {
            return null;
        }

        var lines = (await results.ReadAsync<OrderLine>()).ToList();
        return header.ToDomain(lines);
    }

    private static DateTime? StartOfDayUtc(DateOnly? date) =>
        date?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private sealed record SummaryRow(
        string OrderNumber, string CustomerCode, string CustomerName, string Status, string Source,
        DateTime CreatedAt, decimal TotalAmount, int LineCount)
    {
        public OrderSummary ToDomain() => new(
            OrderNumber, CustomerCode, CustomerName,
            SnakeCase.To<OrderStatus>(Status), SnakeCase.To<OrderSource>(Source),
            SqlText.AsUtc(CreatedAt), TotalAmount, LineCount);
    }

    // A class with settable properties rather than a positional record: Dapper's constructor
    // matching cannot bind Postgres text[] (reported as System.Array) to a string[] parameter.
    private sealed class OrderRow
    {
        public long Id { get; init; }
        public string OrderNumber { get; init; } = "";
        public string CustomerCode { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string Status { get; init; } = "";
        public string Source { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public string? DecidedBy { get; init; }
        public DateTime? DecidedAt { get; init; }
        public string? RejectionReason { get; init; }
        public string? Notes { get; init; }
        public decimal TotalAmount { get; init; }
        public string[] ReviewFlags { get; init; } = [];

        public Order ToDomain(IReadOnlyList<OrderLine> lines) => new(
            Id, OrderNumber, CustomerCode, CustomerName,
            SnakeCase.To<OrderStatus>(Status), SnakeCase.To<OrderSource>(Source),
            CreatedBy, SqlText.AsUtc(CreatedAt), DecidedBy, SqlText.AsUtc(DecidedAt),
            RejectionReason, Notes, TotalAmount, ReviewFlags, lines);
    }
}
