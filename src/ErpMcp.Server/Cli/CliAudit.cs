using System.Diagnostics;
using System.Text.Json;
using ErpMcp.Application.Auditing;
using ErpMcp.Application.Common;
using ErpMcp.Domain.Common;

namespace ErpMcp.Server.Cli;

/// <summary>Audits operator commands that change data, in the same trail as MCP tool calls.</summary>
internal sealed class CliAudit(IAuditLog auditLog, TimeProvider clock)
{
    public const string Channel = "cli";
    private readonly string _sessionId = "cli-" + Guid.NewGuid().ToString("N")[..8];

    public async Task<T> RunAsync<T>(
        string action, string? actor, object arguments, Func<Task<T>> operation, Func<T, string?> entityRef)
    {
        var startedAt = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var outcome = AuditOutcome.Success;
        string? message = null;
        string? reference = null;

        try
        {
            var result = await operation();
            reference = entityRef(result);
            return result;
        }
        catch (Exception ex)
        {
            outcome = AuditOutcomes.Classify(ex);
            message = ex is ErpException or DomainRuleViolationException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
            throw;
        }
        finally
        {
            await auditLog.RecordAsync(new AuditEntry(
                Channel,
                string.IsNullOrWhiteSpace(actor) ? "(none)" : actor.Trim().ToLowerInvariant(),
                _sessionId,
                ClientName: Environment.UserName,
                ClientVersion: null,
                action,
                JsonSerializer.Serialize(arguments),
                outcome,
                message,
                (int)stopwatch.ElapsedMilliseconds,
                reference,
                startedAt), CancellationToken.None);
        }
    }
}
