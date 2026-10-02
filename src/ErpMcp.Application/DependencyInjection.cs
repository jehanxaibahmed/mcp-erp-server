using ErpMcp.Application.Catalog;
using ErpMcp.Application.Customers;
using ErpMcp.Application.Inventory;
using ErpMcp.Application.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CustomerQueries>();
        services.AddScoped<ProductQueries>();
        services.AddScoped<StockQueries>();
        services.AddScoped<OrderQueries>();
        return services;
    }
}
