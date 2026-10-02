namespace ErpMcp.Application.Common;

public sealed record PageRequest
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    private PageRequest(int limit, int offset) => (Limit, Offset) = (limit, offset);

    public int Limit { get; }
    public int Offset { get; }

    public static PageRequest Create(int? limit = null, int? offset = null)
    {
        var l = limit ?? DefaultLimit;
        var o = offset ?? 0;
        Guard.InRange(l, 1, MaxLimit, "limit");
        Guard.InRange(o, 0, int.MaxValue, "offset");
        return new PageRequest(l, o);
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Limit, int Offset)
{
    public bool HasMore => Offset + Items.Count < TotalCount;
}
