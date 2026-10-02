using System.Globalization;
using ErpMcp.Domain.Catalog;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Customers;

namespace ErpMcp.Domain.Orders;

/// <summary>
/// An order proposed by an agent, validated against business rules before it is queued for
/// human approval.
/// </summary>
/// <remarks>
/// Hard rules (inactive customer, discontinued product, silly quantities) reject the draft.
/// Soft concerns (credit limit, stock shortfall) are recorded as <see cref="ReviewFlags"/> so the
/// human approver sees them. The agent is not blocked from proposing the order.
/// </remarks>
public sealed record DraftOrder
{
    public const int MaxLines = 50;
    public const int MaxQuantityPerLine = 10_000;
    public const int MaxNotesLength = 500;

    private static readonly CultureInfo Gbp = CultureInfo.GetCultureInfo("en-GB");

    private DraftOrder(Customer customer, IReadOnlyList<DraftOrderLine> lines, string? notes, IReadOnlyList<string> reviewFlags)
    {
        Customer = customer;
        Lines = lines;
        Notes = notes;
        ReviewFlags = reviewFlags;
    }

    public Customer Customer { get; }
    public IReadOnlyList<DraftOrderLine> Lines { get; }
    public string? Notes { get; }
    public IReadOnlyList<string> ReviewFlags { get; }
    public decimal TotalAmount => Lines.Sum(l => l.LineTotal);

    /// <param name="customer">The ordering customer.</param>
    /// <param name="requestedLines">SKU and quantity pairs, in the order given.</param>
    /// <param name="catalogue">Products for the requested SKUs (missing SKUs are simply absent).</param>
    /// <param name="availableStock">Total available quantity per SKU across all warehouses.</param>
    /// <param name="openOrderValue">Value of the customer's orders that are pending or approved.</param>
    /// <param name="notes">Optional delivery or handling notes.</param>
    public static DraftOrder Create(
        Customer customer,
        IReadOnlyList<(string Sku, int Quantity)> requestedLines,
        IReadOnlyDictionary<string, Product> catalogue,
        IReadOnlyDictionary<string, int> availableStock,
        decimal openOrderValue,
        string? notes)
    {
        if (!customer.CanPlaceOrders)
        {
            throw new DomainRuleViolationException(
                $"Customer {customer.Code} is {customer.Status.ToString().ToLowerInvariant()} and cannot place orders.");
        }

        if (requestedLines.Count == 0)
        {
            throw new DomainRuleViolationException("An order needs at least one line.");
        }

        if (requestedLines.Count > MaxLines)
        {
            throw new DomainRuleViolationException($"An order can have at most {MaxLines} lines.");
        }

        var duplicate = requestedLines.GroupBy(l => l.Sku).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainRuleViolationException(
                $"SKU {duplicate.Key} appears more than once. Combine it into a single line.");
        }

        if (notes is { Length: > MaxNotesLength })
        {
            throw new DomainRuleViolationException($"Notes must be at most {MaxNotesLength} characters.");
        }

        var flags = new List<string>();
        var lines = new List<DraftOrderLine>(requestedLines.Count);
        foreach (var (sku, quantity) in requestedLines)
        {
            if (quantity is < 1 or > MaxQuantityPerLine)
            {
                throw new DomainRuleViolationException(
                    $"Quantity for {sku} must be between 1 and {MaxQuantityPerLine:N0}.");
            }

            if (!catalogue.TryGetValue(sku, out var product))
            {
                throw new DomainRuleViolationException($"Product {sku} does not exist.");
            }

            if (!product.IsActive)
            {
                throw new DomainRuleViolationException($"Product {sku} ({product.Name}) is discontinued.");
            }

            var available = availableStock.GetValueOrDefault(sku);
            if (quantity > available)
            {
                flags.Add($"Insufficient stock for {sku}: requested {quantity}, available {available}.");
            }

            lines.Add(new DraftOrderLine(lines.Count + 1, product, quantity));
        }

        var total = lines.Sum(l => l.LineTotal);
        if (openOrderValue + total > customer.CreditLimit)
        {
            flags.Add(string.Create(Gbp,
                $"Exceeds credit limit: open orders {openOrderValue:C} + this order {total:C} > limit {customer.CreditLimit:C}."));
        }

        return new DraftOrder(customer, lines, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), flags);
    }
}

public sealed record DraftOrderLine(int LineNumber, Product Product, int Quantity)
{
    /// <summary>Price is always taken from the catalogue, never from the requester.</summary>
    public decimal UnitPrice => Product.UnitPrice;

    public decimal LineTotal => UnitPrice * Quantity;
}
