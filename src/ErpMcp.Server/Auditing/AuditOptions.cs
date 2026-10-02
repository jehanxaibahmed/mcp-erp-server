namespace ErpMcp.Server.Auditing;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>What to do when the audit trail cannot be written.</summary>
    public AuditFailureMode FailureMode { get; set; } = AuditFailureMode.Open;

    /// <summary>Also write each audit event as a structured log line (stderr) for log shippers.</summary>
    public bool EmitToLog { get; set; }

    /// <summary>Argument names whose values are replaced with "[redacted]" in the audit trail.</summary>
    public string[] RedactedArguments { get; set; } = [];

    /// <summary>Arguments larger than this are stored as a size marker instead of in full.</summary>
    public int MaxStoredArgumentBytes { get; set; } = 8 * 1024;
}

public enum AuditFailureMode
{
    /// <summary>Calls proceed if the audit write fails; the failure is logged at Error level.</summary>
    Open,

    /// <summary>
    /// A <c>started</c> event must be written before a call runs. If it can't be, the call is refused
    /// and nothing executes.
    /// </summary>
    Closed,
}
