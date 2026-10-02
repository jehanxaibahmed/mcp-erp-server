namespace ErpMcp.Application.Auditing;

/// <summary>One audited action: an MCP tool call or an operator CLI command.</summary>
/// <param name="Channel">Where the action came from: <c>mcp</c> or <c>cli</c>.</param>
/// <param name="Actor">Who acted, e.g. <c>agent:claude-desktop</c> or <c>ops.lead@example.com</c>.</param>
/// <param name="SessionId">Groups actions from one MCP connection or CLI invocation.</param>
/// <param name="ClientName">The MCP client's self-reported name and version, when known.</param>
/// <param name="ClientVersion">The MCP client's self-reported version, when known.</param>
/// <param name="Action">Tool name (<c>get_order</c>) or command (<c>orders.approve</c>).</param>
/// <param name="ArgumentsJson">Arguments as a JSON object, after redaction.</param>
/// <param name="Outcome">How the action ended.</param>
/// <param name="ErrorMessage">The message returned to the caller when the outcome is not success.</param>
/// <param name="DurationMs">Wall-clock time spent handling the action.</param>
/// <param name="EntityRef">Record created or changed, e.g. an order number.</param>
/// <param name="OccurredAt">When the action started.</param>
public sealed record AuditEntry(
    string Channel,
    string Actor,
    string SessionId,
    string? ClientName,
    string? ClientVersion,
    string Action,
    string ArgumentsJson,
    AuditOutcome Outcome,
    string? ErrorMessage,
    int DurationMs,
    string? EntityRef,
    DateTimeOffset OccurredAt);

public enum AuditOutcome
{
    /// <summary>Completed normally.</summary>
    Success,

    /// <summary>Malformed or out-of-range input.</summary>
    Invalid,

    /// <summary>A referenced record does not exist.</summary>
    NotFound,

    /// <summary>A business rule refused the action.</summary>
    Rejected,

    /// <summary>The caller lacked the required permission scope.</summary>
    Denied,

    /// <summary>An unexpected error.</summary>
    Failed,
}
