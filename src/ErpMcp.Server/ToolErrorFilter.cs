using ErpMcp.Application.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server;

/// <summary>
/// Turns expected application failures into tool results with <c>isError: true</c> and a message
/// the agent can act on (for example, fix a malformed SKU and retry). Unexpected exceptions are
/// left to the SDK, which reports a generic error without leaking internals.
/// </summary>
internal static class ToolErrorFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, ct) =>
        {
            try
            {
                return await next(context, ct);
            }
            catch (ErpException ex)
            {
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = ex.Message }],
                };
            }
        };
}
