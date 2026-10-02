namespace ErpMcp.Server.Auditing;

/// <summary>
/// Per-call scratchpad that tools use to add context to their audit entry, such as the number
/// of the order they created. Registered as scoped, so each tool call gets its own.
/// </summary>
public sealed class ToolCallAnnotations
{
    public string? EntityRef { get; set; }
}
