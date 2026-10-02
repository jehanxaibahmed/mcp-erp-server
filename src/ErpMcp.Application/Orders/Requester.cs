namespace ErpMcp.Application.Orders;

/// <summary>Who is asking for a draft.</summary>
/// <param name="Actor">The agent identity, e.g. <c>agent:claude-desktop</c>.</param>
/// <param name="OnBehalfOf">The authenticated person the agent is acting for, when known (HTTP transport).</param>
public sealed record Requester(string Actor, string? OnBehalfOf);
