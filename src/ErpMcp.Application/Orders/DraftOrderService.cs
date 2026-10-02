using ErpMcp.Application.Catalog;
using ErpMcp.Application.Common;
using ErpMcp.Application.Customers;
using ErpMcp.Application.Inventory;
using ErpMcp.Domain.Orders;

namespace ErpMcp.Application.Orders;

public sealed record DraftOrderLineRequest(string Sku, int Quantity);

public sealed record DraftOrderRequest(
    string CustomerCode,
    IReadOnlyList<DraftOrderLineRequest>? Lines,
    string? Notes = null,
    string? IdempotencyKey = null);

/// <param name="Order">The order as stored, including any review flags for the approver.</param>
/// <param name="AlreadyExisted">True when the idempotency key matched an earlier draft and nothing new was created.</param>
public sealed record DraftOrderResult(Order Order, bool AlreadyExisted);

/// <summary>
/// Creates orders on behalf of an agent. Drafts always enter the approval queue. Nothing an agent
/// does here can approve, fulfil or reserve stock.
/// </summary>
public sealed class DraftOrderService(
    ICustomerRepository customers,
    IProductRepository products,
    IStockRepository stock,
    IOrderRepository orders,
    IOrderWriter writer,
    TimeProvider clock)
{
    public async Task<DraftOrderResult> CreateAsync(DraftOrderRequest request, string requestedBy, CancellationToken ct)
    {
        var customerCode = Guard.CustomerCode(request.CustomerCode);
        var key = Guard.IdempotencyKey(request.IdempotencyKey);
        var lines = NormaliseLines(request.Lines);

        if (key is not null && await writer.FindOrderNumberByIdempotencyKeyAsync(key, ct) is { } existingNumber)
        {
            return await ReplayAsync(existingNumber, customerCode, lines, ct);
        }

        var customer = await customers.GetByCodeAsync(customerCode, ct)
            ?? throw new NotFoundException("Customer", customerCode);

        var skus = lines.Select(l => l.Sku).Distinct().ToList();
        var catalogue = (await products.GetBySkusAsync(skus, ct)).ToDictionary(p => p.Sku);
        var available = await stock.GetAvailableBySkusAsync(skus, ct);
        var stats = await customers.GetOrderStatsAsync(customer.Id, ct);

        var draft = DraftOrder.Create(customer, lines, catalogue, available, stats.OpenOrderValue, request.Notes);

        var inserted = await writer.InsertDraftAsync(draft, requestedBy, key, clock.GetUtcNow(), ct);
        if (!inserted.Inserted)
        {
            // Lost a race with a concurrent request that used the same key.
            return await ReplayAsync(inserted.OrderNumber, customerCode, lines, ct);
        }

        return new DraftOrderResult(await LoadAsync(inserted.OrderNumber, ct), AlreadyExisted: false);
    }

    private static List<(string Sku, int Quantity)> NormaliseLines(IReadOnlyList<DraftOrderLineRequest>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new InputValidationException("lines", "at least one line is required.");
        }

        return lines
            .Select((line, i) => (Guard.Sku(line?.Sku, $"lines[{i}].sku"), line?.Quantity ?? 0))
            .ToList();
    }

    private async Task<DraftOrderResult> ReplayAsync(
        string orderNumber, string customerCode, List<(string Sku, int Quantity)> lines, CancellationToken ct)
    {
        var existing = await LoadAsync(orderNumber, ct);
        var sameRequest = existing.CustomerCode == customerCode
            && existing.Lines.Select(l => (l.Sku, l.Quantity)).SequenceEqual(lines);

        if (!sameRequest)
        {
            throw new InputValidationException(
                "idempotencyKey", $"was already used for a different order ({orderNumber}). Use a new key.");
        }

        return new DraftOrderResult(existing, AlreadyExisted: true);
    }

    private async Task<Order> LoadAsync(string orderNumber, CancellationToken ct) =>
        await orders.GetByNumberAsync(orderNumber, ct)
            ?? throw new InvalidOperationException($"Order {orderNumber} vanished after being written.");
}
