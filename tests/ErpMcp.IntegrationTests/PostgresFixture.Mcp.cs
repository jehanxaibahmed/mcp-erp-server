using System.Collections.Concurrent;
using ModelContextProtocol.Client;

namespace ErpMcp.IntegrationTests;

public sealed partial class PostgresFixture
{
    /// <summary>Every scope, for tests that exercise tools rather than permissions.</summary>
    public const string AllScopes = "read orders:draft";

    private readonly ConcurrentDictionary<string, Lazy<Task<McpClient>>> _clients = new();

    /// <summary>
    /// An MCP client connected over stdio to the real server binary, exactly as Claude Desktop or
    /// another host would launch it, granted <paramref name="scopes"/>. One server process per
    /// distinct scope set, shared across tests to avoid paying start-up each time.
    /// </summary>
    public Task<McpClient> GetMcpClientAsync(string scopes = AllScopes) =>
        _clients.GetOrAdd(scopes, s => new Lazy<Task<McpClient>>(() => StartClientAsync(s))).Value;

    private Task<McpClient> StartClientAsync(string scopes) =>
        McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "erp-mcp",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "erp-mcp.dll")],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["Database__ConnectionString"] = ConnectionString,
                ["Database__MigrateOnStartup"] = "false",
                ["Security__Scopes"] = scopes,
            },
        }));

    private async Task DisposeMcpClientAsync()
    {
        foreach (var client in _clients.Values.Where(c => c.IsValueCreated))
        {
            await (await client.Value).DisposeAsync();
        }
    }
}
