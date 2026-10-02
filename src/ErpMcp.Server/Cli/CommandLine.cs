namespace ErpMcp.Server.Cli;

/// <summary>
/// Parsed command line. Tokens of the form <c>--Section:Key=value</c> pass straight through to
/// .NET configuration; everything else is the command, its positional arguments and its options.
/// </summary>
internal sealed record CommandLine(
    IReadOnlyList<string> Command,
    IReadOnlyDictionary<string, string?> Options,
    string[] ConfigurationArgs)
{
    private static readonly HashSet<string> Flags = ["--seed", "--help"];
    private static readonly HashSet<string> ValueOptions = ["--by", "--reason", "--limit"];

    public bool Is(params string[] words) =>
        Command.Count >= words.Length && words.Select((w, i) => Command[i] == w).All(match => match);

    public string? Positional(int index) => index < Command.Count ? Command[index] : null;

    public bool Has(string flag) => Options.ContainsKey(flag);

    public string? Option(string name) => Options.GetValueOrDefault(name);

    public static CommandLine Parse(string[] args)
    {
        var command = new List<string>();
        var options = new Dictionary<string, string?>();
        var configuration = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal) && arg.Contains('=', StringComparison.Ordinal))
            {
                configuration.Add(arg);
            }
            else if (Flags.Contains(arg))
            {
                options[arg] = null;
            }
            else if (ValueOptions.Contains(arg))
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Option {arg} needs a value.");
                }

                options[arg] = args[++i];
            }
            else if (arg.StartsWith('-'))
            {
                throw new ArgumentException($"Unknown option {arg}. Run with --help for usage.");
            }
            else
            {
                command.Add(arg);
            }
        }

        return new CommandLine(command, options, configuration.ToArray());
    }

    public const string Usage = """
        erp-mcp: MCP server exposing a sample wholesale ERP to AI agents.

        Usage:
          erp-mcp                                   Run the MCP server over stdio (default)
          erp-mcp migrate [--seed]                  Apply schema migrations (optionally sample data) and exit

          erp-mcp orders pending [--limit N]        List orders awaiting approval
          erp-mcp orders show <order-number>        Show an order with its lines and review flags
          erp-mcp orders approve <order-number> --by <you@example.com>
          erp-mcp orders reject  <order-number> --by <you@example.com> --reason "<why>"

        Configuration can be overridden with --Section:Key=value, e.g.
          --Database:ConnectionString="Host=...;Database=erp;Username=...;Password=..."
        """;
}
