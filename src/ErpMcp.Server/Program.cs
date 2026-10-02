using ErpMcp.Application;
using ErpMcp.Infrastructure;
using ErpMcp.Server;
using ErpMcp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Usage:
//   erp-mcp                     run the MCP server over stdio
//   erp-mcp migrate [--seed]    apply schema migrations (and optionally sample data), then exit
// Any other --Section:Key=value arguments override configuration as usual.
var command = args.FirstOrDefault() == "migrate" ? "migrate" : "serve";
var seed = args.Contains("--seed");
var configArgs = args.Where(a => a is not ("migrate" or "--seed")).ToArray();

// MCP clients launch the server from arbitrary working directories, so resolve appsettings
// relative to the executable rather than the current directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = configArgs,
    ContentRootPath = AppContext.BaseDirectory,
});

// stdout carries the MCP JSON-RPC stream, so every log line must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

if (command == "migrate")
{
    using var migrationHost = builder.Build();
    migrationHost.Services.GetRequiredService<DatabaseMigrator>().Migrate(seed);
    return;
}

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(serializerOptions: ToolJson.Options)
    .WithRequestFilters(filters => filters.AddCallToolFilter(ToolErrorFilter.Apply));

await builder.Build().RunAsync();
