using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public static class ServerInfoTools
{
    [McpServerTool(Name = "get_server_info", ReadOnly = true, Idempotent = true)]
    [Description("Returns the ERP MCP server name and version. Useful as a connectivity check.")]
    public static ServerInfo GetServerInfo() => new(
        Name: "erp-mcp",
        Version: typeof(ServerInfoTools).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");

    public sealed record ServerInfo(string Name, string Version);
}
