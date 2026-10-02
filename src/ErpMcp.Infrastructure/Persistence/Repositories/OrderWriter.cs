using Dapper;
using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Inventory;
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
        DraftOrder draft, Requester requester, string? idempotencyKey, DateTimeOffset createdAt, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var header = await connection.QuerySingleAsync<(long Id, string OrderNumber)>(new CommandDefinition("""
                INSERT INTO erp.orders (customer_id, status, source, created_by, created_at, notes,
                                        total_amount, review_flags, idempotency_key, on_behalf_of)
                VALUES (@CustomerId, 'pending_approval', 'agent', @CreatedBy, @CreatedAt, @Notes,
                        @TotalAmount, @ReviewFlags, @IdempotencyKey, @OnBehalfOf)
                RETURNING id, order_number
                """,
                new
                {
                    CustomerId = draft.Customer.Id,
                    CreatedBy = requester.Actor,
                    requester.OnBehalfOf,
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

    public async Task<bool> TryRejectAsync(
        long orderId, string decidedBy, string reason, DateTimeOffset decidedAt, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE erp.orders
            SET status = 'rejected', decided_by = @DecidedBy, decided_at = @DecidedAt, rejection_reason = @Reason
            WHERE id = @OrderId AND status = 'pending_approval'
            """,
            new { OrderId = orderId, DecidedBy = decidedBy, DecidedAt = decidedAt.UtcDateTime, Reason = reason },
            cancellationToken: ct));

        return updated == 1;
    }

    public async Task<bool> TryApproveAndReserveAsync(
        long orderId,
        string decidedBy,
        DateTimeOffset decidedAt,
        Func<IReadOnlyList<StockAvailability>, IReadOnlyList<StockAllocation>> allocate,
        CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        if (!await LockOrderInStatusAsync(connection, transaction, orderId, "pending_approval", ct))
        {
            return false;
        }

        // Lock every stock row the order could draw from, in key order so concurrent approvals
        // touching the same products queue up instead of deadlocking.
        var stock = (await connection.QueryAsync<StockAvailability>(new CommandDefinition("""
            SELECT s.product_id AS ProductId, p.sku AS Sku, s.warehouse_id AS WarehouseId, w.code AS WarehouseCode,
                   s.quantity_on_hand - s.quantity_reserved AS Available
            FROM erp.stock_levels s
            JOIN erp.products p ON p.id = s.product_id
            JOIN erp.warehouses w ON w.id = s.warehouse_id
            WHERE s.product_id IN (SELECT product_id FROM erp.order_lines WHERE order_id = @orderId)
            ORDER BY s.product_id, s.warehouse_id
            FOR UPDATE OF s
            """, new { orderId }, transaction, cancellationToken: ct))).ToList();

        var allocations = allocate(stock);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE erp.stock_levels
            SET quantity_reserved = quantity_reserved + @Quantity, updated_at = now()
            WHERE product_id = @ProductId AND warehouse_id = @WarehouseId
            """, allocations, transaction, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO erp.order_allocations (order_id, line_number, product_id, warehouse_id, quantity)
            VALUES (@OrderId, @LineNumber, @ProductId, @WarehouseId, @Quantity)
            """,
            allocations.Select(a => new { OrderId = orderId, a.LineNumber, a.ProductId, a.WarehouseId, a.Quantity }),
            transaction,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE erp.orders SET status = 'approved', decided_by = @DecidedBy, decided_at = @DecidedAt
            WHERE id = @OrderId
            """,
            new { OrderId = orderId, DecidedBy = decidedBy, DecidedAt = decidedAt.UtcDateTime },
            transaction,
            cancellationToken: ct));

        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> TryCloseAsync(
        long orderId, OrderStatus expected, OrderStatus closeAs, string closedBy, string? cancellationReason,
        DateTimeOffset closedAt, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        if (!await LockOrderInStatusAsync(connection, transaction, orderId, SnakeCase.From(expected), ct))
        {
            return false;
        }

        if (expected == OrderStatus.Approved)
        {
            // Fulfilling ships the reserved stock; cancelling puts it back on the shelf.
            var consume = closeAs == OrderStatus.Fulfilled;
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE erp.stock_levels s
                SET quantity_reserved = s.quantity_reserved - a.quantity,
                    quantity_on_hand  = s.quantity_on_hand - CASE WHEN @Consume THEN a.quantity ELSE 0 END,
                    updated_at = now()
                FROM erp.order_allocations a
                WHERE a.order_id = @OrderId AND s.product_id = a.product_id AND s.warehouse_id = a.warehouse_id
                """, new { OrderId = orderId, Consume = consume }, transaction, cancellationToken: ct));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE erp.orders
            SET status = @Status,
                closed_by = @ClosedBy,
                closed_at = @ClosedAt,
                cancellation_reason = @Reason,
                -- Cancelling straight from the approval queue is also the approval decision.
                decided_by = coalesce(decided_by, @ClosedBy),
                decided_at = coalesce(decided_at, @ClosedAt)
            WHERE id = @OrderId
            """,
            new
            {
                OrderId = orderId,
                Status = SnakeCase.From(closeAs),
                ClosedBy = closedBy,
                ClosedAt = closedAt.UtcDateTime,
                Reason = cancellationReason,
            },
            transaction,
            cancellationToken: ct));

        await transaction.CommitAsync(ct);
        return true;
    }

    private static async Task<bool> LockOrderInStatusAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long orderId, string status, CancellationToken ct)
    {
        var current = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT status FROM erp.orders WHERE id = @orderId FOR UPDATE",
            new { orderId },
            transaction,
            cancellationToken: ct));

        return current == status;
    }
}
