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
        DraftOrder draft, string createdBy, string? idempotencyKey, DateTimeOffset createdAt, CancellationToken ct);

    /// <summary>
    /// Records an approval decision only if the order is still pending (optimistic concurrency).
    /// Returns false when someone else decided first.
    /// </summary>
    Task<bool> TryRecordDecisionAsync(
        long orderId, OrderStatus decision, string decidedBy, string? rejectionReason, DateTimeOffset decidedAt, CancellationToken ct);
}

public sealed record DraftInsertResult(string OrderNumber, bool Inserted);
