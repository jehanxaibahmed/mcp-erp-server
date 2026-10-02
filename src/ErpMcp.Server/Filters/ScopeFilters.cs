using ErpMcp.Application.Common;
using ErpMcp.Server.Security;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Filters;

/// <summary>
/// Enforces permission scopes twice: tools the server isn't granted are hidden from
/// <c>tools/list</c> so the model never considers them, and calls to them are refused anyway in
/// case a client calls a tool by name without listing first.
/// </summary>
internal sealed class ScopeFilters(ScopeCatalog catalog, GrantedScopes granted)
{
    public McpRequestHandler<ListToolsRequestParams, ListToolsResult> HideUngrantedTools(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next) =>
        async (context, ct) =>
        {
            var result = await next(context, ct);
            result.Tools = result.Tools.Where(t => catalog.IsToolAllowed(t.Name, granted.Scopes)).ToList();
            return result;
        };

    public McpRequestHandler<CallToolRequestParams, CallToolResult> RefuseUngrantedCalls(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        (context, ct) =>
        {
            var name = context.Params?.Name ?? "";
            if (!catalog.IsToolAllowed(name, granted.Scopes))
            {
                throw new PermissionDeniedException(name, catalog.RequiredScopeForTool(name)!);
            }

            return next(context, ct);
        };

    public McpRequestHandler<ListPromptsRequestParams, ListPromptsResult> HideUngrantedPrompts(
        McpRequestHandler<ListPromptsRequestParams, ListPromptsResult> next) =>
        async (context, ct) =>
        {
            var result = await next(context, ct);
            result.Prompts = result.Prompts.Where(p => catalog.IsPromptAllowed(p.Name, granted.Scopes)).ToList();
            return result;
        };

    /// <summary>
    /// Refuses ungranted prompts, and turns argument validation failures into an
    /// <c>InvalidParams</c> protocol error the client can show the user.
    /// </summary>
    public McpRequestHandler<GetPromptRequestParams, GetPromptResult> GuardPrompts(
        McpRequestHandler<GetPromptRequestParams, GetPromptResult> next) =>
        async (context, ct) =>
        {
            var name = context.Params?.Name ?? "";
            try
            {
                if (!catalog.IsPromptAllowed(name, granted.Scopes))
                {
                    throw new PermissionDeniedException(name, catalog.RequiredScopeForPrompt(name)!);
                }

                return await next(context, ct);
            }
            catch (ErpException ex)
            {
                throw new McpProtocolException(ex.Message, McpErrorCode.InvalidParams);
            }
        };
}
