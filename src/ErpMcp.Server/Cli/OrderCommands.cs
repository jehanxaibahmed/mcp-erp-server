using System.Globalization;
using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.Server.Cli;

/// <summary>
/// Operator commands for the human approval step. These run in a separate process from the MCP
/// server and are never exposed as tools.
/// </summary>
internal static class OrderCommands
{
    private static readonly CultureInfo Gbp = CultureInfo.GetCultureInfo("en-GB");

    public static async Task<int> RunAsync(CommandLine cli, IServiceProvider services, TextWriter output, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var approvals = scope.ServiceProvider.GetRequiredService<OrderApprovalService>();
        var queries = scope.ServiceProvider.GetRequiredService<OrderQueries>();
        var audit = ActivatorUtilities.CreateInstance<CliAudit>(scope.ServiceProvider);

        try
        {
            switch (cli.Positional(1))
            {
                case "pending":
                    var limit = cli.Option("--limit") is { } l ? int.Parse(l, CultureInfo.InvariantCulture) : (int?)null;
                    WritePending(await approvals.ListPendingAsync(limit, ct), output);
                    return 0;

                case "show":
                    WriteOrder(await queries.GetAsync(cli.Positional(2), ct), output);
                    return 0;

                case "approve":
                    var approved = await audit.RunAsync(
                        "orders.approve",
                        cli.Option("--by"),
                        new { orderNumber = cli.Positional(2) },
                        () => approvals.ApproveAsync(cli.Positional(2), cli.Option("--by"), ct),
                        o => o.OrderNumber);
                    output.WriteLine($"Approved {approved.OrderNumber} ({Money(approved.TotalAmount)}) for {approved.CustomerName}.");
                    return 0;

                case "reject":
                    var rejected = await audit.RunAsync(
                        "orders.reject",
                        cli.Option("--by"),
                        new { orderNumber = cli.Positional(2), reason = cli.Option("--reason") },
                        () => approvals.RejectAsync(cli.Positional(2), cli.Option("--by"), cli.Option("--reason"), ct),
                        o => o.OrderNumber);
                    output.WriteLine($"Rejected {rejected.OrderNumber}: {rejected.RejectionReason}");
                    return 0;

                default:
                    output.WriteLine(CommandLine.Usage);
                    return 2;
            }
        }
        catch (Exception ex) when (ex is ErpException or DomainRuleViolationException or FormatException)
        {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static void WritePending(PagedResult<OrderSummary> page, TextWriter output)
    {
        if (page.Items.Count == 0)
        {
            output.WriteLine("No orders are waiting for approval.");
            return;
        }

        output.WriteLine($"{"ORDER",-11} {"CREATED (UTC)",-17} {"SOURCE",-7} {"CUSTOMER",-32} {"LINES",5} {"TOTAL",12}");
        foreach (var o in page.Items)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{o.OrderNumber,-11} {o.CreatedAt.UtcDateTime,-17:yyyy-MM-dd HH:mm} {SnakeCase.From(o.Source),-7} {Truncate($"{o.CustomerCode} {o.CustomerName}", 32),-32} {o.LineCount,5} {Money(o.TotalAmount),12}"));
        }

        output.WriteLine($"{page.Items.Count} of {page.TotalCount} pending.");
    }

    private static void WriteOrder(Order order, TextWriter output)
    {
        output.WriteLine($"{order.OrderNumber}  [{SnakeCase.From(order.Status)}]  {order.CustomerCode} {order.CustomerName}");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Created {order.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC by {order.CreatedBy}"));
        if (order.DecidedBy is not null)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Decided {order.DecidedAt?.UtcDateTime:yyyy-MM-dd HH:mm} UTC by {order.DecidedBy}"));
        }

        if (order.RejectionReason is not null)
        {
            output.WriteLine($"Rejection reason: {order.RejectionReason}");
        }

        if (order.Notes is not null)
        {
            output.WriteLine($"Notes: {order.Notes}");
        }

        output.WriteLine();
        foreach (var line in order.Lines)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {line.LineNumber,2}. {line.Sku}  {Truncate(line.ProductName, 36),-36} {line.Quantity,6} x {Money(line.UnitPrice),9} = {Money(line.LineTotal),10}"));
        }

        output.WriteLine($"  Total: {Money(order.TotalAmount)}");

        if (order.ReviewFlags.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Review flags:");
            foreach (var flag in order.ReviewFlags)
            {
                output.WriteLine($"  ! {flag}");
            }
        }
    }

    private static string Money(decimal amount) => amount.ToString("C", Gbp);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
