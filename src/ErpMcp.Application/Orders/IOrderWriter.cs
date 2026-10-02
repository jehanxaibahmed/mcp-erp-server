using ErpMcp.Domain.Inventory;
using ErpMcp.Domain.Orders;

namespace ErpMcp.Application.Orders;

/// <summary>Write-side port for orders. Kept apart from <see cref="IOrderRepository"/> so read-only callers never get write access.</summary>
public interface IOrderWriter
{
    Task<string?> FindOrderNumberByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    /// <summary>
    /// Inserts the draft as <c>pending_approval</c> in one transaction. If another request already
    /// used <paramref name="idempotencyKey"/>, nothing is inserted and that order's number is returned.
    /// </summary>
    Task<DraftInsertResult> InsertDraftAsync(
        DraftOrder draft, Requester requester, string? idempotencyKey, DateTimeOffset createdAt, CancellationToken ct);

    /// <summary>
    /// Rejects the order only if it is still pending (optimistic concurrency).
    /// Returns false when someone else decided first.
    /// </summary>
    Task<bool> TryRejectAsync(long orderId, string decidedBy, string reason, DateTimeOffset decidedAt, CancellationToken ct);

    /// <summary>
    /// Approves a pending order and reserves its stock in one transaction. The stock rows involved
    /// are locked, then <paramref name="allocate"/> decides where the stock comes from. If it
    /// throws, nothing is changed. Returns false when the order was no longer pending.
    /// </summary>
    Task<bool> TryApproveAndReserveAsync(
        long orderId,
        string decidedBy,
        DateTimeOffset decidedAt,
        Func<IReadOnlyList<StockAvailability>, IReadOnlyList<StockAllocation>> allocate,
        CancellationToken ct);

    /// <summary>
    /// Moves an order from <paramref name="expected"/> to fulfilled or cancelled, consuming or
    /// releasing its reserved stock. Returns false when the order was no longer in <paramref name="expected"/>.
    /// </summary>
    Task<bool> TryCloseAsync(
        long orderId, OrderStatus expected, OrderStatus closeAs, string closedBy, string? cancellationReason,
        DateTimeOffset closedAt, CancellationToken ct);
}

public sealed record DraftInsertResult(string OrderNumber, bool Inserted);
