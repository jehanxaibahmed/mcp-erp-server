namespace ErpMcp.Server;

public enum McpTransport
{
    /// <summary>One local client launches the server as a child process. No user identity.</summary>
    Stdio,

    /// <summary>Streamable HTTP for remote clients, authenticated with OAuth bearer tokens.</summary>
    Http,
}
