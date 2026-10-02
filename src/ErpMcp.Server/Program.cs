using ErpMcp.Application;
using ErpMcp.Application.Common;
using ErpMcp.Infrastructure;
using ErpMcp.Infrastructure.Persistence;
using ErpMcp.Server;
using ErpMcp.Server.Cli;
using ErpMcp.Server.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

CommandLine cli;
try
{
    cli = CommandLine.Parse(args);
}
catch (ArgumentException ex)
{
    await Console.Error.WriteLineAsync($"error: {ex.Message}");
    return 2;
}

if (cli.Has("--help"))
{
    Console.WriteLine(CommandLine.Usage);
    return 0;
}

if (cli.Is("http"))
{
    return await HttpHost.RunAsync(cli);
}

// MCP clients launch the server from arbitrary working directories, so resolve appsettings
// relative to the executable rather than the current directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = cli.ConfigurationArgs,
    ContentRootPath = AppContext.BaseDirectory,
});

// stdout carries the MCP JSON-RPC stream, so every log line must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

if (cli.Is("migrate"))
{
    using var host = builder.Build();
    host.Services.GetRequiredService<DatabaseMigrator>().Migrate(cli.Has("--seed"));
    return 0;
}

if (cli.Is("orders"))
{
    using var host = builder.Build();
    return await OrderCommands.RunAsync(cli, host.Services, Console.Out, CancellationToken.None);
}

if (cli.Is("audit"))
{
    using var host = builder.Build();
    return await AuditCommands.RunAsync(cli, host.Services, Console.Out, CancellationToken.None);
}

if (cli.Command.Count > 0)
{
    await Console.Error.WriteLineAsync($"error: unknown command '{cli.Command[0]}'.\n\n{CommandLine.Usage}");
    return 2;
}

try
{
    builder.Services.AddErpMcpServer(builder.Configuration);
}
catch (ErpException ex)
{
    // Misconfiguration (e.g. an unknown scope) must stop start-up loudly, not degrade silently.
    await Console.Error.WriteLineAsync($"error: {ex.Message}");
    return 1;
}

await builder.Build().RunAsync();
return 0;
