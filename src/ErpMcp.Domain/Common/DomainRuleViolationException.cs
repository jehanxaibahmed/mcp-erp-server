namespace ErpMcp.Domain.Common;

/// <summary>
/// A business rule rejected the operation. The message explains which rule, in terms the
/// requester (human or agent) can act on.
/// </summary>
public sealed class DomainRuleViolationException(string message) : Exception(message);
