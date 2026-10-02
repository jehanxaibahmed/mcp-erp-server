namespace ErpMcp.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Connection used only for migrations, typically the schema owner. Lets the server itself run
    /// as the restricted <c>erp_app</c> role. Defaults to <see cref="ConnectionString"/>.
    /// </summary>
    public string? MigrationConnectionString { get; set; }

    public string EffectiveMigrationConnectionString =>
        string.IsNullOrWhiteSpace(MigrationConnectionString) ? ConnectionString : MigrationConnectionString;

    /// <summary>Apply pending schema migrations when the server starts.</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Load synthetic sample data after migrating. Never enable against real data.</summary>
    public bool SeedSampleData { get; set; }
}
