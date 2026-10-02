using ErpMcp.Application;
using ErpMcp.Infrastructure;
using ErpMcp.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ErpMcp.IntegrationTests.PostgresFixture))]

namespace ErpMcp.IntegrationTests;

/// <summary>
/// One disposable PostgreSQL container per test run, migrated and seeded with the sample data.
/// Tests that write data must use their own records (or clean up) so they can run in any order.
/// </summary>
public sealed partial class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("erp")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    /// <summary>The real application + infrastructure wiring, pointed at the test database.</summary>
    public IServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        CreateMigrator().Migrate(seedSampleData: true);
        DataSource = NpgsqlDataSource.Create(ConnectionString);
        Services = CreateServices(ConnectionString);
    }

    public T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>A connection string for the test database as a different login.</summary>
    public string ConnectionStringFor(string username, string password) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Username = username, Password = password }.ConnectionString;

    /// <summary>Application services wired to the given connection, e.g. a least-privilege login.</summary>
    public static ServiceProvider CreateServices(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }

    public DatabaseMigrator CreateMigrator() => new(
        Options.Create(new DatabaseOptions { ConnectionString = ConnectionString }),
        NullLogger<DatabaseMigrator>.Instance);

    public async ValueTask DisposeAsync()
    {
        await DisposeMcpClientAsync();

        if (Services is IAsyncDisposable services)
        {
            await services.DisposeAsync();
        }

        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
