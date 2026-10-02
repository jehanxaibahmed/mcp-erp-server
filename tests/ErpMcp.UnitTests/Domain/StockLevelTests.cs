using ErpMcp.Domain.Inventory;

namespace ErpMcp.UnitTests.Domain;

public class StockLevelTests
{
    [Theory]
    [InlineData(100, 10, 50, 90, false)]
    [InlineData(60, 15, 50, 45, true)]
    [InlineData(50, 0, 50, 50, false)]
    public void Available_stock_accounts_for_reservations(
        int onHand, int reserved, int reorderLevel, int expectedAvailable, bool expectedLow)
    {
        var level = new StockLevel("BEV-0001", "Coffee", "WH-MAN", "Manchester", onHand, reserved, reorderLevel);

        level.QuantityAvailable.ShouldBe(expectedAvailable);
        level.IsBelowReorderLevel.ShouldBe(expectedLow);
    }
}
