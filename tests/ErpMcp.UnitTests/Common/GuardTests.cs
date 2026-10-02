using ErpMcp.Application.Common;
using ErpMcp.Domain.Orders;

namespace ErpMcp.UnitTests.Common;

public class GuardTests
{
    [Theory]
    [InlineData("CUST-0001", "CUST-0001")]
    [InlineData("  cust-0042 ", "CUST-0042")]
    public void CustomerCode_normalises_valid_codes(string input, string expected) =>
        Guard.CustomerCode(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CUST-1")]
    [InlineData("CUS-0001")]
    [InlineData("CUST-0001; DROP TABLE erp.customers")]
    public void CustomerCode_rejects_invalid_input(string? input)
    {
        var ex = Should.Throw<InputValidationException>(() => Guard.CustomerCode(input));
        ex.Field.ShouldBe("customerCode");
    }

    [Theory]
    [InlineData("bev-0001", "BEV-0001")]
    [InlineData("MEA-0004", "MEA-0004")]
    public void Sku_normalises_valid_codes(string input, string expected) =>
        Guard.Sku(input).ShouldBe(expected);

    [Theory]
    [InlineData("BEV0001")]
    [InlineData("BE-0001")]
    [InlineData("BEV-00001")]
    public void Sku_rejects_invalid_codes(string input) =>
        Should.Throw<InputValidationException>(() => Guard.Sku(input));

    [Fact]
    public void OrderNumber_requires_SO_prefix() =>
        Should.Throw<InputValidationException>(() => Guard.OrderNumber("PO-100001"));

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  coffee ", "coffee")]
    public void SearchText_trims_and_treats_blank_as_absent(string? input, string? expected) =>
        Guard.SearchText(input).ShouldBe(expected);

    [Fact]
    public void SearchText_rejects_overly_long_input() =>
        Should.Throw<InputValidationException>(() => Guard.SearchText(new string('x', Guard.MaxSearchLength + 1)));

    [Theory]
    [InlineData("pending_approval", OrderStatus.PendingApproval)]
    [InlineData("PendingApproval", OrderStatus.PendingApproval)]
    [InlineData("FULFILLED", OrderStatus.Fulfilled)]
    public void OptionalEnum_accepts_snake_case_and_pascal_case(string input, OrderStatus expected) =>
        Guard.OptionalEnum<OrderStatus>(input, "status").ShouldBe(expected);

    [Fact]
    public void OptionalEnum_lists_allowed_values_on_failure()
    {
        var ex = Should.Throw<InputValidationException>(() => Guard.OptionalEnum<OrderStatus>("shipped", "status"));
        ex.Message.ShouldContain("pending_approval");
        ex.Message.ShouldContain("cancelled");
    }

    [Fact]
    public void OptionalEnum_rejects_numeric_values_outside_the_enum() =>
        Should.Throw<InputValidationException>(() => Guard.OptionalEnum<OrderStatus>("42", "status"));
}
