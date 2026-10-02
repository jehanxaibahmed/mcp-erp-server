using System.Text.Json;
using System.Text.Json.Nodes;
using ErpMcp.Application.Auditing;
using ErpMcp.Application.Common;
using ErpMcp.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Auditing;

/// <summary>
/// Records every tool call, including denied, invalid and failed ones, in the audit trail.
/// </summary>
/// <remarks>
/// It sits just inside the error filter, so it sees the original exception type (denied, invalid,
/// rejected…) before that exception becomes an <c>isError</c> result. If the audit write itself
/// fails, the call's result is still returned and the failure is logged at Error level. The
/// alternative, failing a read because the log is down, would make the audit store a single
/// point of failure.
/// </remarks>
public sealed partial class ToolCallAuditor(
    IAuditLog auditLog,
    AuditOptions options,
    AuditSession session,
    TimeProvider clock,
    ILogger<ToolCallAuditor> logger)
{
    public const string Channel = "mcp";

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        (context, ct) => context.Services!.GetRequiredService<ToolCallAuditor>().InvokeAsync(context, next, ct);

    public async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> context,
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        CancellationToken ct)
    {
        var startedAt = clock.GetUtcNow();
        var startTimestamp = clock.GetTimestamp();
        CallToolResult? result = null;
        Exception? failure = null;

        try
        {
            result = await next(context, ct);
            return result;
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            var (outcome, message) = Describe(result, failure);
            var client = context.Server.ClientInfo;
            var entry = new AuditEntry(
                Channel,
                AgentActor.FromClientName(client?.Name),
                session.Id,
                client?.Name,
                client?.Version,
                context.Params?.Name ?? "(unknown)",
                SerialiseArguments(context.Params?.Arguments),
                outcome,
                message,
                (int)clock.GetElapsedTime(startTimestamp).TotalMilliseconds,
                context.Services?.GetService<ToolCallAnnotations>()?.EntityRef,
                startedAt);

            await TryRecordAsync(entry);
        }
    }

    private async Task TryRecordAsync(AuditEntry entry)
    {
        try
        {
            // Not the request's token: a cancelled call must still be audited.
            await auditLog.RecordAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogAuditWriteFailed(ex, entry.Action, entry.Outcome);
        }
    }

    private static (AuditOutcome Outcome, string? Message) Describe(CallToolResult? result, Exception? failure) =>
        failure switch
        {
            ErpException or DomainRuleViolationException => (AuditOutcomes.Classify(failure), failure.Message),
            not null => (AuditOutcome.Failed, $"{failure.GetType().Name}: {failure.Message}"),
            null when result?.IsError == true => (AuditOutcome.Failed, result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text),
            null => (AuditOutcome.Success, null),
        };

    private string SerialiseArguments(IDictionary<string, JsonElement>? arguments)
    {
        var json = new JsonObject();
        foreach (var (name, value) in arguments ?? new Dictionary<string, JsonElement>())
        {
            json[name] = options.RedactedArguments.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? "[redacted]"
                : JsonNode.Parse(value.GetRawText());
        }

        var serialised = json.ToJsonString();
        return serialised.Length <= options.MaxStoredArgumentBytes
            ? serialised
            : new JsonObject { ["_truncated"] = true, ["_bytes"] = serialised.Length }.ToJsonString();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "AUDIT WRITE FAILED for {Action} ({Outcome}). The call completed but is missing from the audit trail")]
    private partial void LogAuditWriteFailed(Exception ex, string action, AuditOutcome outcome);
}
