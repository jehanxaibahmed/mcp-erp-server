namespace ErpMcp.Application.Common;

/// <summary>The caller lacks the scope an operation requires.</summary>
public sealed class PermissionDeniedException(string operation, string requiredScope)
    : ErpException($"'{operation}' requires the '{requiredScope}' scope, which this server has not been granted. Ask an administrator to enable it.")
{
    public string RequiredScope { get; } = requiredScope;
}
