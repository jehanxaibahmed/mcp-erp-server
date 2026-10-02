using ErpMcp.Application.Catalog;
using ErpMcp.Application.Customers;
using ErpMcp.Application.Inventory;
using ErpMcp.Application.Orders;
using ErpMcp.Infrastructure.Persistence;
using ErpMcp.Infrastructure.Persistence.Repositories;
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
        services.AddSingleton<ICustomerRepository, CustomerRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IStockRepository, StockRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddHostedService<MigrationHostedService>();

        return services;
    }
}
