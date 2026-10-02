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
    public Task<McpClient> GetMcpClientAsync(string scopes = AllScopes, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var key = scopes + "|" + string.Join(";", (environment ?? new Dictionary<string, string?>()).OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"));
        return _clients.GetOrAdd(key, _ => new Lazy<Task<McpClient>>(() => StartClientAsync(scopes, environment))).Value;
    }

    private Task<McpClient> StartClientAsync(string scopes, IReadOnlyDictionary<string, string?>? environment)
    {
        var variables = new Dictionary<string, string?>
        {
            ["Database__ConnectionString"] = ConnectionString,
            ["Database__MigrateOnStartup"] = "false",
            ["Security__Scopes"] = scopes,
            ["Audit__RedactedArguments__0"] = "notes",
        };

        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            variables[name] = value;
        }

        return McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "erp-mcp",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "erp-mcp.dll")],
            EnvironmentVariables = variables,
        }));
    }

    private async Task DisposeMcpClientAsync()
    {
        foreach (var client in _clients.Values.Where(c => c.IsValueCreated))
        {
            await (await client.Value).DisposeAsync();
        }
    }
}
