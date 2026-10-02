using ModelContextProtocol.Protocol;
using Npgsql;

namespace ErpMcp.IntegrationTests.EndToEnd;

/// <summary>
/// Starts servers as a login that can read ERP data but cannot write the audit trail, to show
/// the difference between fail-open and fail-closed auditing.
/// </summary>
public sealed class McpAuditFailureModeTests(PostgresFixture db) : IAsyncLifetime
{
    private const string Role = "noaudit_e2e";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{Role}') THEN
                    CREATE ROLE {Role} LOGIN PASSWORD 'x';
                    GRANT USAGE ON SCHEMA erp TO {Role};
                    GRANT SELECT ON ALL TABLES IN SCHEMA erp TO {Role};
                END IF;
            END $$;
            """, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<ModelContextProtocol.Client.McpClient> ClientAsync(string mode) =>
        db.GetMcpClientAsync("read", new Dictionary<string, string?>
        {
            ["Database__ConnectionString"] = db.ConnectionStringFor(Role, "x"),
            ["Audit__FailureMode"] = mode,
        });

    [Fact]
    public async Task Fail_open_still_answers_when_the_audit_trail_is_unwritable()
    {
        var client = await ClientAsync("Open");

        var result = await client.CallToolAsync("get_product", new Dictionary<string, object?> { ["sku"] = "BEV-0001" }, cancellationToken: Ct);

        result.IsError.ShouldNotBe(true);
    }

    [Fact]
    public async Task Fail_closed_refuses_before_executing()
    {
        var client = await ClientAsync("Closed");

        var result = await client.CallToolAsync("get_product", new Dictionary<string, object?> { ["sku"] = "BEV-0001" }, cancellationToken: Ct);

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain("audit trail is unavailable");
    }
}
