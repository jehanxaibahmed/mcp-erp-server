using ErpMcp.Application.Auditing;
using Npgsql;

namespace ErpMcp.IntegrationTests.Repositories;

public class AuditLogTests(PostgresFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IAuditLog _audit = db.GetService<IAuditLog>();

    private static AuditEntry Entry(string action, AuditOutcome outcome = AuditOutcome.Success) => new(
        "mcp", "agent:tests", "session-1", "tests", "1.0", action, """{"sku":"BEV-0001"}""",
        outcome, outcome == AuditOutcome.Success ? null : "nope", 12, null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Records_and_reads_back_entries()
    {
        var action = "test_" + Guid.NewGuid().ToString("N")[..8];
        await _audit.RecordAsync(Entry(action, AuditOutcome.Denied), Ct);

        var record = (await _audit.ListRecentAsync(new AuditQuery(10, Action: action), Ct)).ShouldHaveSingleItem();

        record.Entry.Outcome.ShouldBe(AuditOutcome.Denied);
        record.Entry.ErrorMessage.ShouldBe("nope");
        record.Entry.ArgumentsJson.ShouldBe("""{"sku": "BEV-0001"}""");
    }

    [Fact]
    public async Task Filters_by_outcome()
    {
        var action = "test_" + Guid.NewGuid().ToString("N")[..8];
        await _audit.RecordAsync(Entry(action), Ct);
        await _audit.RecordAsync(Entry(action, AuditOutcome.Invalid), Ct);

        var invalid = await _audit.ListRecentAsync(new AuditQuery(10, action, AuditOutcome.Invalid), Ct);

        invalid.ShouldHaveSingleItem().Entry.Outcome.ShouldBe(AuditOutcome.Invalid);
    }

    [Theory]
    [InlineData("UPDATE audit.events SET outcome = 'success'")]
    [InlineData("DELETE FROM audit.events")]
    [InlineData("TRUNCATE audit.events")]
    public async Task History_cannot_be_rewritten(string sql)
    {
        await _audit.RecordAsync(Entry("test_tamper"), Ct);
        await using var command = db.DataSource.CreateCommand(sql);

        var ex = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));

        ex.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        ex.MessageText.ShouldContain("append-only");
    }
}
