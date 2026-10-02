namespace ErpMcp.Application.Common;

/// <summary>
/// Base for expected, client-facing failures. Messages are shown to the calling agent, so they
/// must be actionable and must never contain internal details (SQL, stack traces, secrets).
/// </summary>
public abstract class ErpException(string message) : Exception(message);

/// <summary>An argument was missing, malformed or out of range.</summary>
public sealed class InputValidationException(string field, string problem)
    : ErpException($"Invalid '{field}': {problem}")
{
    public string Field { get; } = field;
}

/// <summary>The referenced record does not exist.</summary>
public sealed class NotFoundException(string entity, string key)
    : ErpException($"{entity} '{key}' was not found.");
