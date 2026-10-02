namespace ErpMcp.Application.Auditing;

/// <summary>Append-only audit trail.</summary>
public interface IAuditLog
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct);

    Task<IReadOnlyList<AuditRecord>> ListRecentAsync(AuditQuery query, CancellationToken ct);
}

public sealed record AuditQuery(int Limit, string? Action = null, AuditOutcome? Outcome = null, string? Actor = null);

public sealed record AuditRecord(long Id, AuditEntry Entry);
