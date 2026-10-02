using Npgsql;

namespace ErpMcp.IntegrationTests.Persistence;

public class DatabaseMigratorTests(PostgresFixture db)
{
    [Fact]
    public void Migrating_again_is_a_no_op() =>
        Should.NotThrow(() => db.CreateMigrator().Migrate(seedSampleData: true));

    [Theory]
    [InlineData("erp.customers", 20)]
    [InlineData("erp.products", 30)]
    [InlineData("erp.warehouses", 3)]
    // Other tests add drafts, so count only the seeded orders.
    [InlineData("erp.orders WHERE created_by IN ('agent:sample', 'sales.team@example.com')", 60)]
    public async Task Sample_data_is_loaded(string tableAndFilter, long expectedRows)
    {
        (await ScalarAsync<long>($"SELECT count(*) FROM {tableAndFilter}")).ShouldBe(expectedRows);
    }

    [Fact]
    public async Task Order_totals_match_their_lines()
    {
        var mismatches = await ScalarAsync<long>("""
            SELECT count(*)
            FROM erp.orders o
            WHERE o.total_amount <> (SELECT coalesce(sum(line_total), 0) FROM erp.order_lines l WHERE l.order_id = o.id)
            """);

        mismatches.ShouldBe(0);
    }

    [Fact]
    public async Task Pending_orders_cannot_carry_a_decision()
    {
        var ex = await Should.ThrowAsync<PostgresException>(() => ScalarAsync<long>("""
            UPDATE erp.orders SET decided_at = now()
            WHERE status = 'pending_approval'
            RETURNING id
            """));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Reserved_stock_cannot_exceed_on_hand()
    {
        var ex = await Should.ThrowAsync<PostgresException>(() => ScalarAsync<long>("""
            UPDATE erp.stock_levels SET quantity_reserved = quantity_on_hand + 1
            RETURNING product_id
            """));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = db.DataSource.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
