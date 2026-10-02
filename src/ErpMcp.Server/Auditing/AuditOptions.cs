namespace ErpMcp.Server.Auditing;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Argument names whose values are replaced with "[redacted]" in the audit trail.</summary>
    public string[] RedactedArguments { get; set; } = [];

    /// <summary>Arguments larger than this are stored as a size marker instead of in full.</summary>
    public int MaxStoredArgumentBytes { get; set; } = 8 * 1024;
}
