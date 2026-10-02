using ErpMcp.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ErpMcp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Database:ConnectionString is required.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
        });

        services.AddSingleton<DatabaseMigrator>();
        services.AddHostedService<MigrationHostedService>();

        return services;
    }
}
