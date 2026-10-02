namespace ErpMcp.Server.Security;

/// <summary>Declares the permission scope an MCP tool needs. Every tool must carry one (enforced by tests and at start-up).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresScopeAttribute(string scope) : Attribute
{
    /// <summary>Marker for tools that expose no ERP data, such as connectivity checks.</summary>
    public const string None = "none";

    public string Scope { get; } = scope;
}
