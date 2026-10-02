using System.ComponentModel;
using System.Reflection;
using ErpMcp.Server.Security;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class ServerInfoTools(ScopeResolver scopes)
{
    [RequiresScope(RequiresScopeAttribute.None)]
    [McpServerTool(Name = "get_server_info", Title = "Server info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the server version, the permission scopes in force for this caller, and who the caller is acting for. Useful as a connectivity check.")]
    public ServerInfo GetServerInfo(RequestContext<CallToolRequestParams> context) => new(
        Name: "erp-mcp",
        Version: typeof(ServerInfoTools).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
        GrantedScopes: scopes.Resolve(context.User).Order(StringComparer.Ordinal).ToList(),
        OnBehalfOf: CallerIdentity.PersonFrom(context.User));

    public sealed record ServerInfo(string Name, string Version, IReadOnlyList<string> GrantedScopes, string? OnBehalfOf);
}
