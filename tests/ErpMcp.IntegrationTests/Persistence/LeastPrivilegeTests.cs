using ErpMcp.Application.Auditing;
using ErpMcp.Application.Catalog;
using ErpMcp.Application.Orders;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ErpMcp.IntegrationTests.Persistence;

/// <summary>
/// Runs the application as a login in the <c>erp_app</c> group role created by migration 0005,
/// proving it can do its job and nothing more.
/// </summary>
public sealed class LeastPrivilegeTests(PostgresFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];
    private string _appConnection = "";
    private string _auditorConnection = "";

    public async ValueTask InitializeAsync()
    {
        await ExecuteAsOwnerAsync($"CREATE ROLE app_{_suffix} LOGIN PASSWORD 'app' IN ROLE erp_app");
        await ExecuteAsOwnerAsync($"CREATE ROLE auditor_{_suffix} LOGIN PASSWORD 'auditor' IN ROLE erp_auditor");
        _appConnection = db.ConnectionStringFor($"app_{_suffix}", "app");
        _auditorConnection = db.ConnectionStringFor($"auditor_{_suffix}", "auditor");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task App_role_can_run_the_whole_order_workflow()
    {
        await using var services = PostgresFixture.CreateServices(_appConnection);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        (await sp.GetRequiredService<ProductQueries>().SearchAsync("coffee", null, false, null, null, Ct)).TotalCount.ShouldBeGreaterThan(0);
        var draft = await sp.GetRequiredService<DraftOrderService>().CreateAsync(
            new DraftOrderRequest("CUST-0008", [new("CLN-0001", 1)]), "agent:tests", Ct);
        await sp.GetRequiredService<OrderApprovalService>().ApproveAsync(draft.Order.OrderNumber, "ops@example.com", Ct);
        var fulfilled = await sp.GetRequiredService<OrderFulfilmentService>().FulfilAsync(draft.Order.OrderNumber, "wh@example.com", Ct);
        await sp.GetRequiredService<IAuditLog>().RecordAsync(new AuditEntry(
            "cli", "ops@example.com", "s", null, null, "orders.fulfil", "{}", AuditOutcome.Success, null, 1, fulfilled.OrderNumber, DateTimeOffset.UtcNow), Ct);

        fulfilled.Status.ShouldBe(ErpMcp.Domain.Orders.OrderStatus.Fulfilled);
    }

    [Theory]
    [InlineData("UPDATE audit.events SET outcome = 'success'")]
    [InlineData("DELETE FROM audit.events")]
    [InlineData("TRUNCATE audit.events")]
    [InlineData("CREATE TABLE erp.backdoor (id int)")]
    [InlineData("DROP TABLE erp.orders")]
    [InlineData("ALTER TABLE erp.orders DISABLE TRIGGER ALL")]
    [InlineData("ALTER TABLE audit.events DISABLE TRIGGER audit_events_append_only")]
    [InlineData("DELETE FROM erp.customers")]
    [InlineData("SELECT count(*) FROM public.schema_versions")]
    public async Task App_role_is_refused(string sql)
    {
        var ex = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(_appConnection, sql));

        ex.SqlState.ShouldBeOneOf(PostgresErrorCodes.InsufficientPrivilege, "42501");
    }

    [Fact]
    public async Task Auditor_can_read_the_trail_but_not_write_or_see_erp_data()
    {
        await ExecuteAsync(_auditorConnection, "SELECT count(*) FROM audit.events");

        (await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(_auditorConnection,
            "INSERT INTO audit.events (occurred_at, channel, actor, session_id, action, outcome, duration_ms) VALUES (now(), 'cli', 'x', 'x', 'x', 'success', 0)")))
            .SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(_auditorConnection, "SELECT count(*) FROM erp.customers")))
            .SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    private Task ExecuteAsOwnerAsync(string sql) => ExecuteAsync(db.ConnectionString, sql);

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
