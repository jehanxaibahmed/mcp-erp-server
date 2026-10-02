using ErpMcp.Server.Cli;

namespace ErpMcp.UnitTests.Cli;

public class CommandLineTests
{
    [Fact]
    public void Separates_command_options_and_configuration()
    {
        var cli = CommandLine.Parse(
            ["orders", "reject", "SO-100001", "--by", "ops@example.com", "--reason", "Out of range", "--Database:ConnectionString=Host=x"]);

        cli.Is("orders", "reject").ShouldBeTrue();
        cli.Positional(2).ShouldBe("SO-100001");
        cli.Option("--by").ShouldBe("ops@example.com");
        cli.Option("--reason").ShouldBe("Out of range");
        cli.ConfigurationArgs.ShouldBe(["--Database:ConnectionString=Host=x"]);
    }

    [Fact]
    public void No_arguments_means_serve()
    {
        var cli = CommandLine.Parse([]);
        cli.Command.ShouldBeEmpty();
        cli.ConfigurationArgs.ShouldBeEmpty();
    }

    [Fact]
    public void Flags_are_recognised() =>
        CommandLine.Parse(["migrate", "--seed"]).Has("--seed").ShouldBeTrue();

    [Fact]
    public void Value_option_without_value_is_an_error() =>
        Should.Throw<ArgumentException>(() => CommandLine.Parse(["orders", "approve", "SO-1", "--by"]));

    [Fact]
    public void Unknown_options_are_an_error() =>
        Should.Throw<ArgumentException>(() => CommandLine.Parse(["--force"]));
}
