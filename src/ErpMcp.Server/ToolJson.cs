using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace ErpMcp.Server;

/// <summary>JSON settings for tool arguments and results.</summary>
internal static class ToolJson
{
    /// <summary>
    /// The SDK defaults plus enums as snake_case strings ("pending_approval"), matching the
    /// vocabulary agents use when passing filters back in.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        // Insert first: the SDK defaults already contain an enum converter, and the first match wins.
        options.Converters.Insert(0, new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
