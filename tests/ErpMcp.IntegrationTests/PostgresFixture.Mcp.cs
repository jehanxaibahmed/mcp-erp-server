using ModelContextProtocol.Client;

namespace ErpMcp.IntegrationTests;

public sealed partial class PostgresFixture
{
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private McpClient? _client;

    /// <summary>
    /// An MCP client connected over stdio to the real server binary, exactly as Claude Desktop or
    /// another host would launch it. Shared across tests to avoid paying process start-up each time.
    /// </summary>
    public async Task<McpClient> GetMcpClientAsync()
    {
        await _clientLock.WaitAsync();
        try
        {
            return _client ??= await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "erp-mcp",
                Command = "dotnet",
                Arguments = [Path.Combine(AppContext.BaseDirectory, "erp-mcp.dll")],
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["Database__ConnectionString"] = ConnectionString,
                    ["Database__MigrateOnStartup"] = "false",
                },
            }));
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private async Task DisposeMcpClientAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        _clientLock.Dispose();
    }
}
