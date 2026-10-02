using ErpMcp.Server.Filters;
using ErpMcp.Server.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.Server;

internal static class McpServerSetup
{
    /// <summary>
    /// Registers the MCP server: stdio transport, tools, and the filter pipeline.
    /// Call-tool filters run outermost first:
    /// errors → scopes → argument validation → tool.
    /// </summary>
    public static IServiceCollection AddErpMcpServer(this IServiceCollection services, IConfiguration configuration)
    {
        var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
        var granted = new GrantedScopes(security.GrantedScopes());
        var catalog = ToolCatalog.FromAssembly(typeof(McpServerSetup).Assembly);

        services.AddSingleton(security);
        services.AddSingleton(granted);
        services.AddSingleton(catalog);

        var scopeFilters = new ScopeFilters(catalog, granted);
        var argumentFilter = new ArgumentValidationFilter(security);

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(serializerOptions: ToolJson.Options)
            .WithRequestFilters(filters =>
            {
                filters.AddListToolsFilter(scopeFilters.HideUngrantedTools);
                filters.AddCallToolFilter(ToolErrorFilter.Apply);
                filters.AddCallToolFilter(scopeFilters.RefuseUngrantedCalls);
                filters.AddCallToolFilter(argumentFilter.Apply);
            });

        return services;
    }
}
