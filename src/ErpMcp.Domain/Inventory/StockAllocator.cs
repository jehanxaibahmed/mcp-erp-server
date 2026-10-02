using ErpMcp.Domain.Common;

namespace ErpMcp.Domain.Inventory;

/// <summary>Stock of one product in one warehouse that can still be promised.</summary>
public sealed record StockAvailability(long ProductId, string Sku, long WarehouseId, string WarehouseCode, int Available);

/// <summary>Quantity of one order line reserved in one warehouse.</summary>
public sealed record StockAllocation(int LineNumber, long ProductId, string Sku, long WarehouseId, string WarehouseCode, int Quantity);

/// <summary>A quantity of a product that needs to be reserved for an order line.</summary>
public sealed record AllocationRequest(int LineNumber, string Sku, int Quantity);

/// <summary>
/// Decides which warehouses fulfil each order line.
/// </summary>
/// <remarks>
/// Lines are served from a single warehouse when one can cover the whole line, because one
/// shipment is cheaper than several. Choose the one with the most stock, to keep the others
/// balanced. Otherwise the line is split, drawing from the best-stocked warehouses first.
/// Lines for the same SKU draw from a shared pool so stock is never promised twice.
/// </remarks>
public static class StockAllocator
{
    public static IReadOnlyList<StockAllocation> Allocate(
        string orderNumber, IReadOnlyList<AllocationRequest> lines, IReadOnlyList<StockAvailability> stock)
    {
        var shortfalls = lines
            .GroupBy(l => l.Sku)
            .Select(g => (Sku: g.Key, Needed: g.Sum(l => l.Quantity), Available: stock.Where(s => s.Sku == g.Key).Sum(s => s.Available)))
            .Where(x => x.Needed > x.Available)
            .ToList();

        if (shortfalls.Count > 0)
        {
            var detail = string.Join("; ", shortfalls.Select(s => $"{s.Sku} needs {s.Needed}, available {s.Available}"));
            throw new DomainRuleViolationException(
                $"Cannot approve {orderNumber}: insufficient stock ({detail}). Reject it or wait for stock to arrive.");
        }

        var remaining = stock.ToDictionary(s => (s.Sku, s.WarehouseId), s => s.Available);
        var allocations = new List<StockAllocation>();

        foreach (var line in lines.OrderBy(l => l.LineNumber))
        {
            var candidates = stock
                .Where(s => s.Sku == line.Sku)
                .OrderByDescending(s => remaining[(s.Sku, s.WarehouseId)])
                .ThenBy(s => s.WarehouseCode, StringComparer.Ordinal)
                .ToList();

            var single = candidates.FirstOrDefault(s => remaining[(s.Sku, s.WarehouseId)] >= line.Quantity);
            var sources = single is not null ? [single] : candidates;

            var outstanding = line.Quantity;
            foreach (var source in sources)
            {
                var take = Math.Min(outstanding, remaining[(source.Sku, source.WarehouseId)]);
                if (take == 0)
                {
                    continue;
                }

                remaining[(source.Sku, source.WarehouseId)] -= take;
                outstanding -= take;
                allocations.Add(new StockAllocation(line.LineNumber, source.ProductId, source.Sku, source.WarehouseId, source.WarehouseCode, take));

                if (outstanding == 0)
                {
                    break;
                }
            }
        }

        return allocations;
    }
}
