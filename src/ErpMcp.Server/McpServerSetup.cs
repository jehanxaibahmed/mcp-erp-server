using ErpMcp.Server.Auditing;
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
    /// errors → audit → scopes → argument validation → tool.
    /// Audit sits inside the error filter so it sees the original exception (denied, invalid…)
    /// and outside the checks so refused calls are recorded too.
    /// </summary>
    public static IServiceCollection AddErpMcpServer(
        this IServiceCollection services, IConfiguration configuration, McpTransport transport = McpTransport.Stdio)
    {
        var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
        var granted = new GrantedScopes(security.GrantedScopes());
        var catalog = ScopeCatalog.FromAssembly(typeof(McpServerSetup).Assembly);

        services.AddSingleton(security);
        services.AddSingleton(granted);
        services.AddSingleton(catalog);
        var resolver = new ScopeResolver(granted, requireAuthenticatedUser: transport == McpTransport.Http);
        services.AddSingleton(resolver);

        services.AddSingleton(configuration.GetSection(AuditOptions.SectionName).Get<AuditOptions>() ?? new AuditOptions());
        services.AddSingleton(AuditSession.New());
        services.AddSingleton<ToolCallAuditor>();
        services.AddScoped<ToolCallAnnotations>();

        var scopeFilters = new ScopeFilters(catalog, resolver);
        var argumentFilter = new ArgumentValidationFilter(security);

        var mcp = services.AddMcpServer();
        _ = transport == McpTransport.Http ? mcp.WithHttpTransport() : mcp.WithStdioServerTransport();

        mcp
            .WithToolsFromAssembly(serializerOptions: ToolJson.Options)
            .WithPromptsFromAssembly()
            .WithResourcesFromAssembly()
            .WithRequestFilters(filters =>
            {
                filters.AddListToolsFilter(scopeFilters.HideUngrantedTools);
                filters.AddListPromptsFilter(scopeFilters.HideUngrantedPrompts);
                filters.AddGetPromptFilter(scopeFilters.GuardPrompts);
                filters.AddCallToolFilter(ToolErrorFilter.Apply);
                filters.AddCallToolFilter(ToolCallAuditor.Filter);
                filters.AddCallToolFilter(scopeFilters.RefuseUngrantedCalls);
                filters.AddCallToolFilter(argumentFilter.Apply);
            });

        return services;
    }
}
