using ErpMcp.Application.Common;

namespace ErpMcp.UnitTests.Common;

public class AgentActorTests
{
    [Theory]
    [InlineData("claude-ai", "agent:claude-ai")]
    [InlineData("Claude Desktop", "agent:claude-desktop")]
    [InlineData("evil\n<script>; DROP", "agent:evilscript-drop")]
    [InlineData(null, "agent:unknown")]
    [InlineData("   ", "agent:unknown")]
    public void Sanitises_client_names(string? clientName, string expected) =>
        AgentActor.FromClientName(clientName).ShouldBe(expected);

    [Fact]
    public void Caps_length() =>
        AgentActor.FromClientName(new string('a', 500)).Length.ShouldBe(AgentActor.Prefix.Length + 50);
}
