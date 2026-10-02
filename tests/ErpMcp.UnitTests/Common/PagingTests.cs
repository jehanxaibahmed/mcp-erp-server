using ErpMcp.Application.Common;

namespace ErpMcp.UnitTests.Common;

public class PagingTests
{
    [Fact]
    public void Defaults_apply_when_not_specified()
    {
        var page = PageRequest.Create();
        page.Limit.ShouldBe(PageRequest.DefaultLimit);
        page.Offset.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(101, 0)]
    [InlineData(10, -1)]
    public void Out_of_range_values_are_rejected(int limit, int offset) =>
        Should.Throw<InputValidationException>(() => PageRequest.Create(limit, offset));

    [Theory]
    [InlineData(0, 10, 25, true)]
    [InlineData(20, 5, 25, false)]
    [InlineData(0, 0, 0, false)]
    public void HasMore_reflects_remaining_items(int offset, int returned, int total, bool expected)
    {
        var result = new PagedResult<int>(Enumerable.Range(0, returned).ToList(), total, 10, offset);
        result.HasMore.ShouldBe(expected);
    }
}
