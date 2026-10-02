using System.Text.Json;
using ErpMcp.Application.Auditing;
using ErpMcp.Application.Common;
using ErpMcp.Server.Auditing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace ErpMcp.UnitTests.Auditing;

public class ToolCallAuditorTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];
        public bool Broken { get; set; }

        public Task RecordAsync(AuditEntry entry, CancellationToken ct)
        {
            if (Broken)
            {
                throw new InvalidOperationException("database down");
            }

            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditRecord>> ListRecentAsync(AuditQuery query, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private readonly FakeAuditLog _log = new();
    private int _executions;

    private ToolCallAuditor Auditor(AuditFailureMode mode = AuditFailureMode.Open, params string[] redact) => new(
        _log,
        new AuditOptions { FailureMode = mode, RedactedArguments = redact, MaxStoredArgumentBytes = 200 },
        new AuditSession("test-session"),
        TimeProvider.System,
        NullLogger<ToolCallAuditor>.Instance);

    private static ToolCall Call(string json = """{"sku":"BEV-0001"}""", string? entityRef = null) => new(
        "get_product",
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json),
        "Claude Desktop",
        "1.2.3",
        () => entityRef);

    private Func<CancellationToken, ValueTask<CallToolResult>> Succeeds() => _ =>
    {
        _executions++;
        return ValueTask.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = "ok" }] });
    };

    private Func<CancellationToken, ValueTask<CallToolResult>> Throws(Exception ex) => _ =>
    {
        _executions++;
        throw ex;
    };

    [Fact]
    public async Task Records_one_success_event_with_identity_in_open_mode()
    {
        await Auditor().InvokeAsync(Call(entityRef: "SO-1"), Succeeds(), CancellationToken.None);

        var entry = _log.Entries.ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.Actor.ShouldBe("agent:claude-desktop"),
            e => e.ClientVersion.ShouldBe("1.2.3"),
            e => e.SessionId.ShouldBe("test-session"),
            e => e.EntityRef.ShouldBe("SO-1"),
            e => e.ArgumentsJson.ShouldBe("""{"sku":"BEV-0001"}"""));
    }

    [Fact]
    public async Task Classifies_and_rethrows_expected_failures()
    {
        var ex = new InputValidationException("sku", "bad");

        await Should.ThrowAsync<InputValidationException>(() => Auditor().InvokeAsync(Call(), Throws(ex), CancellationToken.None).AsTask());

        var entry = _log.Entries.ShouldHaveSingleItem();
        entry.Outcome.ShouldBe(AuditOutcome.Invalid);
        entry.ErrorMessage.ShouldBe(ex.Message);
    }

    [Fact]
    public async Task Unexpected_failures_keep_the_exception_type_for_investigation()
    {
        await Should.ThrowAsync<TimeoutException>(() =>
            Auditor().InvokeAsync(Call(), Throws(new TimeoutException("db slow")), CancellationToken.None).AsTask());

        _log.Entries.Single().ErrorMessage.ShouldBe("TimeoutException: db slow");
    }

    [Fact]
    public async Task Open_mode_returns_the_result_when_the_audit_write_fails()
    {
        _log.Broken = true;

        var result = await Auditor().InvokeAsync(Call(), Succeeds(), CancellationToken.None);

        result.IsError.ShouldNotBe(true);
        _executions.ShouldBe(1);
    }

    [Fact]
    public async Task Closed_mode_refuses_without_executing_when_the_audit_trail_is_down()
    {
        _log.Broken = true;

        await Should.ThrowAsync<AuditUnavailableException>(() =>
            Auditor(AuditFailureMode.Closed).InvokeAsync(Call(), Succeeds(), CancellationToken.None).AsTask());

        _executions.ShouldBe(0);
    }

    [Fact]
    public async Task Closed_mode_records_started_then_outcome_with_one_call_id()
    {
        await Auditor(AuditFailureMode.Closed).InvokeAsync(Call(), Succeeds(), CancellationToken.None);

        _log.Entries.Select(e => e.Outcome).ShouldBe([AuditOutcome.Started, AuditOutcome.Success]);
        _log.Entries.Select(e => e.CallId).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Redacts_configured_arguments_case_insensitively()
    {
        await Auditor(redact: "NOTES").InvokeAsync(Call("""{"notes":"gate 1234","sku":"BEV-0001"}"""), Succeeds(), CancellationToken.None);

        _log.Entries.Single().ArgumentsJson.ShouldBe("""{"notes":"[redacted]","sku":"BEV-0001"}""");
    }

    [Fact]
    public async Task Oversized_arguments_are_stored_as_a_marker()
    {
        var big = JsonSerializer.Serialize(new { query = new string('x', 500) });

        await Auditor().InvokeAsync(Call(big), Succeeds(), CancellationToken.None);

        _log.Entries.Single().ArgumentsJson.ShouldStartWith("""{"_truncated":true""");
    }
}
