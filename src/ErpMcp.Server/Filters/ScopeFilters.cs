using ErpMcp.Application.Common;
using ErpMcp.Server.Security;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Filters;

/// <summary>
/// Enforces permission scopes twice: tools the server isn't granted are hidden from
/// <c>tools/list</c> so the model never considers them, and calls to them are refused anyway in
/// case a client calls a tool by name without listing first.
/// </summary>
internal sealed class ScopeFilters(ToolCatalog catalog, GrantedScopes granted)
{
    public McpRequestHandler<ListToolsRequestParams, ListToolsResult> HideUngrantedTools(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next) =>
        async (context, ct) =>
        {
            var result = await next(context, ct);
            result.Tools = result.Tools.Where(t => catalog.IsAllowed(t.Name, granted.Scopes)).ToList();
            return result;
        };

    public McpRequestHandler<CallToolRequestParams, CallToolResult> RefuseUngrantedCalls(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        (context, ct) =>
        {
            var name = context.Params?.Name ?? "";
            if (!catalog.IsAllowed(name, granted.Scopes))
            {
                throw new PermissionDeniedException(name, catalog.RequiredScope(name)!);
            }

            return next(context, ct);
        };
}
