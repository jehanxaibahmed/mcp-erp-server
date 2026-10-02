using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ErpMcp.Infrastructure.Persistence;

/// <summary>Runs migrations before the MCP transport starts accepting requests, when enabled.</summary>
internal sealed class MigrationHostedService(DatabaseMigrator migrator, IOptions<DatabaseOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.MigrateOnStartup)
        {
            migrator.Migrate(options.Value.SeedSampleData);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
