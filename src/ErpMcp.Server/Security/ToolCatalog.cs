using System.Reflection;
using ErpMcp.Application.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Security;

/// <summary>Maps each MCP tool name to the scope it requires, discovered from tool attributes.</summary>
public sealed class ToolCatalog
{
    private readonly Dictionary<string, string> _scopeByTool;

    private ToolCatalog(Dictionary<string, string> scopeByTool) => _scopeByTool = scopeByTool;

    public IReadOnlyDictionary<string, string> ScopeByTool => _scopeByTool;

    public static ToolCatalog FromAssembly(Assembly assembly)
    {
        var scopes = new Dictionary<string, string>(StringComparer.Ordinal);
        var methods = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => (Method: m, Tool: m.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(x => x.Tool is not null);

        foreach (var (method, tool) in methods)
        {
            var name = tool!.Name ?? method.Name;
            var scope = method.GetCustomAttribute<RequiresScopeAttribute>()?.Scope
                ?? throw new InvalidOperationException($"Tool '{name}' ({method.DeclaringType!.Name}.{method.Name}) has no [RequiresScope].");

            if (scope != RequiresScopeAttribute.None && !Scopes.All.Contains(scope))
            {
                throw new InvalidOperationException($"Tool '{name}' requires unknown scope '{scope}'.");
            }

            scopes.Add(name, scope);
        }

        return new ToolCatalog(scopes);
    }

    /// <summary>Unknown tools are allowed through so the SDK can answer with its normal "tool not found" error.</summary>
    public bool IsAllowed(string toolName, IReadOnlySet<string> granted) =>
        !_scopeByTool.TryGetValue(toolName, out var scope) || scope == RequiresScopeAttribute.None || granted.Contains(scope);

    public string? RequiredScope(string toolName) => _scopeByTool.GetValueOrDefault(toolName);
}
