using ErpMcp.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ErpMcp.IntegrationTests.PostgresFixture))]

namespace ErpMcp.IntegrationTests;

/// <summary>
/// One disposable PostgreSQL container per test run, migrated and seeded with the sample data.
/// Tests that write data should do so inside a transaction they roll back.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("erp")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        CreateMigrator().Migrate(seedSampleData: true);
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    public DatabaseMigrator CreateMigrator() => new(
        Options.Create(new DatabaseOptions { ConnectionString = ConnectionString }),
        NullLogger<DatabaseMigrator>.Instance);

    public async ValueTask DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
