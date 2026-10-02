namespace ErpMcp.Server.Auditing;

/// <summary>
/// Identifies this process's session in the audit trail. Over stdio one process serves exactly one
/// client connection, so a per-process id groups everything an agent did in one conversation.
/// </summary>
public sealed record AuditSession(string Id)
{
    public static AuditSession New() => new(Guid.NewGuid().ToString("N")[..12]);
}
