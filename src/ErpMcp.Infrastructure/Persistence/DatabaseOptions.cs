namespace ErpMcp.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "";

    /// <summary>Apply pending schema migrations when the server starts.</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Load synthetic sample data after migrating. Never enable against real data.</summary>
    public bool SeedSampleData { get; set; }
}
