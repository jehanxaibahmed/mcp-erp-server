using System.Text;

namespace ErpMcp.Application.Common;

/// <summary>Identity recorded for actions taken through MCP, e.g. <c>agent:claude-desktop</c>.</summary>
public static class AgentActor
{
    public const string Prefix = "agent:";

    /// <summary>Builds an actor id from the MCP client's self-reported name, keeping only safe characters.</summary>
    public static string FromClientName(string? clientName)
    {
        var safe = new StringBuilder();
        foreach (var c in (clientName ?? "").Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            {
                safe.Append(c);
            }
            else if (c == ' ' && safe.Length > 0 && safe[^1] != '-')
            {
                safe.Append('-');
            }

            if (safe.Length == 50)
            {
                break;
            }
        }

        return Prefix + (safe.Length == 0 ? "unknown" : safe.ToString());
    }
}
