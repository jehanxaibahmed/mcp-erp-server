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

/// <summary>What the auditor needs to know about a tool call, independent of the SDK's request types.</summary>
/// <param name="ToolName">The tool being called.</param>
/// <param name="Arguments">Raw arguments as sent by the client.</param>
/// <param name="ClientName">MCP client name from <c>initialize</c>.</param>
/// <param name="ClientVersion">MCP client version from <c>initialize</c>.</param>
/// <param name="EntityRef">Read after the call, so tools can report what they created.</param>
public sealed record ToolCall(
    string ToolName,
    IDictionary<string, JsonElement>? Arguments,
    string? ClientName,
    string? ClientVersion,
    Func<string?> EntityRef);

/// <summary>
/// Records every tool call, including denied, invalid and failed ones, in the audit trail.
/// </summary>
/// <remarks>
/// It sits just inside the error filter, so it sees the original exception type (denied, invalid,
/// rejected…) before that exception becomes an <c>isError</c> result.
/// <para>
/// <see cref="AuditFailureMode.Open"/> (default): one event per call, written afterwards. If the write
/// fails the result is still returned and <c>AUDIT WRITE FAILED</c> is logged at Error level.
/// </para>
/// <para>
/// <see cref="AuditFailureMode.Closed"/>: a <c>started</c> event is written first. If that fails, the
/// call is refused before anything runs. The outcome event follows with the same call id.
/// </para>
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
        (context, ct) => context.Services!.GetRequiredService<ToolCallAuditor>().InvokeAsync(
            new ToolCall(
                context.Params?.Name ?? "(unknown)",
                context.Params?.Arguments,
                context.Server.ClientInfo?.Name,
                context.Server.ClientInfo?.Version,
                () => context.Services?.GetService<ToolCallAnnotations>()?.EntityRef),
            token => next(context, token),
            ct);

    public async ValueTask<CallToolResult> InvokeAsync(
        ToolCall call, Func<CancellationToken, ValueTask<CallToolResult>> next, CancellationToken ct)
    {
        var callId = Guid.NewGuid();
        var startedAt = clock.GetUtcNow();
        var startTimestamp = clock.GetTimestamp();
        var arguments = SerialiseArguments(call.Arguments);

        if (options.FailureMode == AuditFailureMode.Closed
            && !await TryRecordAsync(Entry(call, callId, arguments, AuditOutcome.Started, null, 0, null, startedAt)))
        {
            throw new AuditUnavailableException();
        }

        CallToolResult? result = null;
        Exception? failure = null;
        try
        {
            result = await next(ct);
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
            var duration = (int)clock.GetElapsedTime(startTimestamp).TotalMilliseconds;
            await TryRecordAsync(Entry(call, callId, arguments, outcome, message, duration, call.EntityRef(), startedAt));
        }
    }

    private AuditEntry Entry(
        ToolCall call, Guid callId, string arguments, AuditOutcome outcome, string? message, int durationMs,
        string? entityRef, DateTimeOffset occurredAt) =>
        new(
            Channel,
            AgentActor.FromClientName(call.ClientName),
            session.Id,
            call.ClientName,
            call.ClientVersion,
            call.ToolName,
            arguments,
            outcome,
            message,
            durationMs,
            entityRef,
            occurredAt)
        {
            CallId = callId,
        };

    private async Task<bool> TryRecordAsync(AuditEntry entry)
    {
        if (options.EmitToLog)
        {
            LogAuditEvent(entry.Channel, entry.Actor, entry.Action, entry.Outcome, entry.DurationMs, entry.EntityRef, entry.CallId, entry.SessionId);
        }

        try
        {
            // Not the request's token: a cancelled call must still be audited.
            await auditLog.RecordAsync(entry, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            LogAuditWriteFailed(ex, entry.Action, entry.Outcome);
            return false;
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

    [LoggerMessage(Level = LogLevel.Error, Message = "AUDIT WRITE FAILED for {Action} ({Outcome}). The event is missing from the audit trail")]
    private partial void LogAuditWriteFailed(Exception ex, string action, AuditOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, EventName = "AuditEvent",
        Message = "audit {Channel} {Actor} {Action} {Outcome} {DurationMs}ms ref={EntityRef} call={CallId} session={SessionId}")]
    private partial void LogAuditEvent(
        string channel, string actor, string action, AuditOutcome outcome, int durationMs, string? entityRef, Guid callId, string sessionId);
}
