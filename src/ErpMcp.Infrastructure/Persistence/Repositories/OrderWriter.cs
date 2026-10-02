using Dapper;
using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Orders;
using Npgsql;

namespace ErpMcp.Infrastructure.Persistence.Repositories;

internal sealed class OrderWriter(NpgsqlDataSource db) : IOrderWriter
{
    private const string IdempotencyIndex = "ux_orders_idempotency_key";

    public async Task<string?> FindOrderNumberByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT order_number FROM erp.orders WHERE idempotency_key = @idempotencyKey",
            new { idempotencyKey },
            cancellationToken: ct));
    }

    public async Task<DraftInsertResult> InsertDraftAsync(
        DraftOrder draft, string createdBy, string? idempotencyKey, DateTimeOffset createdAt, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var header = await connection.QuerySingleAsync<(long Id, string OrderNumber)>(new CommandDefinition("""
                INSERT INTO erp.orders (customer_id, status, source, created_by, created_at, notes,
                                        total_amount, review_flags, idempotency_key)
                VALUES (@CustomerId, 'pending_approval', 'agent', @CreatedBy, @CreatedAt, @Notes,
                        @TotalAmount, @ReviewFlags, @IdempotencyKey)
                RETURNING id, order_number
                """,
                new
                {
                    CustomerId = draft.Customer.Id,
                    CreatedBy = createdBy,
                    CreatedAt = createdAt.UtcDateTime,
                    draft.Notes,
                    draft.TotalAmount,
                    ReviewFlags = draft.ReviewFlags.ToArray(),
                    IdempotencyKey = idempotencyKey,
                },
                transaction,
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO erp.order_lines (order_id, line_number, product_id, quantity, unit_price)
                VALUES (@OrderId, @LineNumber, @ProductId, @Quantity, @UnitPrice)
                """,
                draft.Lines.Select(l => new
                {
                    OrderId = header.Id,
                    l.LineNumber,
                    ProductId = l.Product.Id,
                    l.Quantity,
                    l.UnitPrice,
                }),
                transaction,
                cancellationToken: ct));

            await transaction.CommitAsync(ct);
            return new DraftInsertResult(header.OrderNumber, Inserted: true);
        }
        catch (PostgresException ex) when (ex is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IdempotencyIndex })
        {
            await transaction.RollbackAsync(ct);
            var existing = await FindOrderNumberByIdempotencyKeyAsync(idempotencyKey!, ct)
                ?? throw new InvalidOperationException("Idempotency key conflict but no matching order.", ex);
            return new DraftInsertResult(existing, Inserted: false);
        }
    }

    public async Task<bool> TryRecordDecisionAsync(
        long orderId, OrderStatus decision, string decidedBy, string? rejectionReason, DateTimeOffset decidedAt, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE erp.orders
            SET status = @Status, decided_by = @DecidedBy, decided_at = @DecidedAt, rejection_reason = @RejectionReason
            WHERE id = @OrderId AND status = 'pending_approval'
            """,
            new
            {
                OrderId = orderId,
                Status = SnakeCase.From(decision),
                DecidedBy = decidedBy,
                DecidedAt = decidedAt.UtcDateTime,
                RejectionReason = rejectionReason,
            },
            cancellationToken: ct));

        return updated == 1;
    }
}
