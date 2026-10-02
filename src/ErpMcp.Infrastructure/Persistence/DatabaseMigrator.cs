using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpMcp.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded SQL scripts with DbUp. Schema migrations and sample data are journaled
/// separately so a production database can take migrations without ever receiving seed data.
/// </summary>
public sealed partial class DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
{
    private const string ScriptNamespace = "ErpMcp.Infrastructure.Persistence.Scripts.";
    private static readonly Assembly ScriptAssembly = typeof(DatabaseMigrator).Assembly;

    public void Migrate(bool seedSampleData)
    {
        var connectionString = options.Value.EffectiveMigrationConnectionString;
        // DbUp's default log writes to stdout, which is reserved for the MCP protocol stream.
        EnsureDatabase.For.PostgresqlDatabase(connectionString, new NoOpUpgradeLog());

        Run("schema migrations", connectionString, ScriptNamespace + "Migrations.", "schema_versions");
        if (seedSampleData)
        {
            Run("sample data", connectionString, ScriptNamespace + "Seed.", "seed_versions");
        }
    }

    private void Run(string description, string connectionString, string prefix, string journalTable)
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(ScriptAssembly, name => name.StartsWith(prefix, StringComparison.Ordinal))
            .JournalToPostgresqlTable("public", journalTable)
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        var pending = upgrader.GetScriptsToExecute();
        if (pending.Count == 0)
        {
            LogUpToDate(description);
            return;
        }

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Applying {description} failed at script '{result.ErrorScript?.Name}'.", result.Error);
        }

        var applied = string.Join(", ", pending.Select(s => s.Name[prefix.Length..]));
        LogApplied(pending.Count, description, applied);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database {Description} are up to date")]
    private partial void LogUpToDate(string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied {Count} {Description} script(s): {Scripts}")]
    private partial void LogApplied(int count, string description, string scripts);
}
