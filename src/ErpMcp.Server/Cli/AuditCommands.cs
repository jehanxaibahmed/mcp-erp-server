using System.Globalization;
using ErpMcp.Application.Auditing;
using ErpMcp.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.Server.Cli;

internal static class AuditCommands
{
    public static async Task<int> RunAsync(CommandLine cli, IServiceProvider services, TextWriter output, CancellationToken ct)
    {
        try
        {
            var limit = Guard.InRange(
                cli.Option("--limit") is { } l ? int.Parse(l, CultureInfo.InvariantCulture) : 50, 1, 1000, "limit");
            var query = new AuditQuery(
                limit,
                cli.Option("--action"),
                Guard.OptionalEnum<AuditOutcome>(cli.Option("--outcome"), "outcome"),
                cli.Option("--actor")?.ToLowerInvariant());

            var events = await services.GetRequiredService<IAuditLog>().ListRecentAsync(query, ct);
            Write(events, output);
            return 0;
        }
        catch (Exception ex) when (ex is ErpException or FormatException)
        {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static void Write(IReadOnlyList<AuditRecord> events, TextWriter output)
    {
        if (events.Count == 0)
        {
            output.WriteLine("No audit events match.");
            return;
        }

        output.WriteLine($"{"TIME (UTC)",-19} {"CH",-3} {"ACTOR",-26} {"ACTION",-24} {"OUTCOME",-9} {"MS",5}  DETAIL");
        foreach (var (_, e) in events)
        {
            var detail = e.EntityRef ?? e.ErrorMessage ?? (e.ArgumentsJson == "{}" ? "" : e.ArgumentsJson);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{e.OccurredAt.UtcDateTime,-19:yyyy-MM-dd HH:mm:ss} {e.Channel,-3} {Fit(e.Actor, 26),-26} {Fit(e.Action, 24),-24} {SnakeCase.From(e.Outcome),-9} {e.DurationMs,5}  {Fit(detail, 70)}"));
        }
    }

    private static string Fit(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
