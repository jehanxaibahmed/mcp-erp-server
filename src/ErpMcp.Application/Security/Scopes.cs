using ErpMcp.Application.Common;

namespace ErpMcp.Application.Security;

/// <summary>
/// Permission scopes. Each MCP tool requires exactly one; a server instance is granted a set of
/// them by configuration. Reads and writes are separate scopes so read-only is the safe default.
/// </summary>
public static class Scopes
{
    public const string CustomersRead = "customers:read";
    public const string CatalogRead = "catalog:read";
    public const string InventoryRead = "inventory:read";
    public const string OrdersRead = "orders:read";
    public const string OrdersDraft = "orders:draft";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CustomersRead, CatalogRead, InventoryRead, OrdersRead, OrdersDraft,
    };

    /// <summary>Every <c>*:read</c> scope and nothing that writes.</summary>
    public static IReadOnlySet<string> ReadOnly { get; } =
        All.Where(s => s.EndsWith(":read", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Parses a space- or comma-separated scope list (OAuth style). The shorthand <c>read</c>
    /// expands to <see cref="ReadOnly"/>. Unknown scopes are an error, so a typo in configuration
    /// fails at start-up instead of silently granting less (or more) than intended.
    /// </summary>
    public static IReadOnlySet<string> Parse(string? value)
    {
        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in (value ?? "").Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var scope = token.ToLowerInvariant();
            if (scope == "read")
            {
                granted.UnionWith(ReadOnly);
            }
            else if (All.Contains(scope))
            {
                granted.Add(scope);
            }
            else
            {
                throw new InputValidationException("Security:Scopes", $"unknown scope '{token}'. Known scopes: {string.Join(", ", All.Order())}.");
            }
        }

        return granted;
    }
}
