using System.ComponentModel;
using System.Reflection;
using ErpMcp.Server.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class ServerInfoTools(GrantedScopes granted)
{
    [RequiresScope(RequiresScopeAttribute.None)]
    [McpServerTool(Name = "get_server_info", Title = "Server info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the server version and the permission scopes it has been granted. Useful as a connectivity check.")]
    public ServerInfo GetServerInfo() => new(
        Name: "erp-mcp",
        Version: typeof(ServerInfoTools).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
        GrantedScopes: granted.Scopes.Order().ToList());

    public sealed record ServerInfo(string Name, string Version, IReadOnlyList<string> GrantedScopes);
}
