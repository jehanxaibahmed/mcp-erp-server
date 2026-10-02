using System.Text.Json;
using ErpMcp.Application.Common;
using ErpMcp.Server.Security;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Filters;

/// <summary>
/// Checks the shape of tool arguments before a tool runs. The SDK binds known parameters and
/// silently ignores the rest, so a misspelled filter such as <c>customer_code</c> would otherwise
/// widen a query to every customer. Unknown arguments are rejected with a suggestion instead.
/// </summary>
internal sealed class ArgumentValidationFilter(SecurityOptions options)
{
    public McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        (context, ct) =>
        {
            if (context.MatchedPrimitive is McpServerTool tool && context.Params?.Arguments is { Count: > 0 } arguments)
            {
                EnsureWithinSizeLimit(arguments);
                EnsureKnownArguments(tool.ProtocolTool, arguments.Keys);
            }

            return next(context, ct);
        };

    private void EnsureWithinSizeLimit(IDictionary<string, JsonElement> arguments)
    {
        var size = arguments.Sum(a => a.Key.Length + a.Value.GetRawText().Length);
        if (size > options.MaxArgumentBytes)
        {
            throw new InputValidationException("arguments", $"are {size:N0} bytes; the limit is {options.MaxArgumentBytes:N0}.");
        }
    }

    private static void EnsureKnownArguments(Tool tool, IEnumerable<string> supplied)
    {
        var known = tool.InputSchema.TryGetProperty("properties", out var properties)
            ? properties.EnumerateObject().Select(p => p.Name).ToList()
            : [];

        foreach (var name in supplied)
        {
            if (known.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            var suggestion = known.FirstOrDefault(k => Normalise(k) == Normalise(name));
            var hint = suggestion is not null
                ? $" Did you mean '{suggestion}'?"
                : known.Count > 0 ? $" Valid arguments: {string.Join(", ", known)}." : " This tool takes no arguments.";

            throw new InputValidationException(name, $"is not an argument of '{tool.Name}'.{hint}");
        }
    }

    private static string Normalise(string name) =>
        name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
}
