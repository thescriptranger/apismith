using ApiSmith.Naming;

namespace ApiSmith.UnitTests.Naming;

public sealed class CasingTests
{
    [Theory]
    [InlineData("order_items", "OrderItems")]
    [InlineData("OrderItems", "OrderItems")]
    [InlineData("orderItems", "OrderItems")]
    [InlineData("ORDER_ITEMS", "OrderItems")]
    [InlineData("order-items", "OrderItems")]
    [InlineData("order item", "OrderItem")]
    [InlineData("dbo.order_items", "DboOrderItems")]
    [InlineData("a", "A")]
    [InlineData("", "")]
    [InlineData("123abc", "_123abc")]
    public void ToPascal_handles_common_inputs(string input, string expected)
    {
        Assert.Equal(expected, Casing.ToPascal(input));
    }

    [Theory]
    [InlineData("order_items", "orderItems")]
    [InlineData("Customer", "customer")]
    [InlineData("customer", "customer")]
    public void ToCamel_lowers_first_char(string input, string expected)
    {
        Assert.Equal(expected, Casing.ToCamel(input));
    }
}
