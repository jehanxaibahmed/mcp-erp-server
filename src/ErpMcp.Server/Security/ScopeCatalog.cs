using System.Reflection;
using ErpMcp.Application.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Security;

/// <summary>
/// Maps each MCP tool and prompt to the scope it requires, discovered from <see cref="RequiresScopeAttribute"/>.
/// </summary>
public sealed class ScopeCatalog
{
    private ScopeCatalog(Dictionary<string, string> tools, Dictionary<string, string> prompts) =>
        (ScopeByTool, ScopeByPrompt) = (tools, prompts);

    public IReadOnlyDictionary<string, string> ScopeByTool { get; }

    public IReadOnlyDictionary<string, string> ScopeByPrompt { get; }

    public static ScopeCatalog FromAssembly(Assembly assembly) => new(
        Discover<McpServerToolTypeAttribute, McpServerToolAttribute>(assembly, a => a.Name),
        Discover<McpServerPromptTypeAttribute, McpServerPromptAttribute>(assembly, a => a.Name));

    /// <summary>Unknown names are allowed through so the SDK can answer with its normal "not found" error.</summary>
    public bool IsToolAllowed(string toolName, IReadOnlySet<string> granted) => IsAllowed(ScopeByTool, toolName, granted);

    public bool IsPromptAllowed(string promptName, IReadOnlySet<string> granted) => IsAllowed(ScopeByPrompt, promptName, granted);

    public string? RequiredScopeForTool(string toolName) => ScopeByTool.GetValueOrDefault(toolName);

    public string? RequiredScopeForPrompt(string promptName) => ScopeByPrompt.GetValueOrDefault(promptName);

    private static bool IsAllowed(IReadOnlyDictionary<string, string> scopes, string name, IReadOnlySet<string> granted) =>
        !scopes.TryGetValue(name, out var scope) || scope == RequiresScopeAttribute.None || granted.Contains(scope);

    private static Dictionary<string, string> Discover<TTypeAttribute, TMemberAttribute>(
        Assembly assembly, Func<TMemberAttribute, string?> nameOf)
        where TTypeAttribute : Attribute
        where TMemberAttribute : Attribute
    {
        var scopes = new Dictionary<string, string>(StringComparer.Ordinal);
        var methods = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<TMemberAttribute>()))
            .Where(x => x.Attribute is not null);

        foreach (var (method, attribute) in methods)
        {
            var name = nameOf(attribute!) ?? method.Name;
            var scope = method.GetCustomAttribute<RequiresScopeAttribute>()?.Scope
                ?? throw new InvalidOperationException($"'{name}' ({method.DeclaringType!.Name}.{method.Name}) has no [RequiresScope].");

            if (scope != RequiresScopeAttribute.None && !Scopes.All.Contains(scope))
            {
                throw new InvalidOperationException($"'{name}' requires unknown scope '{scope}'.");
            }

            scopes.Add(name, scope);
        }

        return scopes;
    }
}
