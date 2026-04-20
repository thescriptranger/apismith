using ApiSmith.Introspection.Readers;

namespace ApiSmith.Introspection.Tests.Readers;

public sealed class CheckConstraintsReaderTests
{
    [Fact]
    public void GroupRows_empty_input_returns_empty_dictionary()
    {
        var result = CheckConstraintsReader.GroupRows(
            System.Array.Empty<(string, string, string, string)>());

        Assert.Empty(result);
    }

    [Fact]
    public void GroupRows_single_constraint_preserves_expression_verbatim()
    {
        var rows = new[]
        {
            ("dbo", "People", "CK_People_Age", "([Age] >= (0))"),
        };

        var result = CheckConstraintsReader.GroupRows(rows);

        var constraints = Assert.Contains(("dbo", "People"), result);
        var ck = Assert.Single(constraints);
        Assert.Equal("CK_People_Age", ck.Name);
        Assert.Equal("([Age] >= (0))", ck.Expression);
    }

    [Fact]
    public void GroupRows_multiple_constraints_on_same_table_sorted_by_name_ordinal()
    {
        var rows = new[]
        {
            ("dbo", "People", "CK_Zeta",  "([Zeta] > (0))"),
            ("dbo", "People", "CK_Alpha", "([Alpha] > (0))"),
            ("dbo", "People", "CK_Mike",  "([Mike] > (0))"),
        };

        var result = CheckConstraintsReader.GroupRows(rows);

        var constraints = Assert.Contains(("dbo", "People"), result);
        Assert.Collection(constraints,
            c => Assert.Equal("CK_Alpha", c.Name),
            c => Assert.Equal("CK_Mike",  c.Name),
            c => Assert.Equal("CK_Zeta",  c.Name));
    }

    [Fact]
    public void GroupRows_constraints_on_two_tables_produce_two_entries()
    {
        var rows = new[]
        {
            ("dbo", "People",  "CK_People_Age",  "([Age] >= (0))"),
            ("dbo", "Orders",  "CK_Orders_Total", "([Total] > (0))"),
        };

        var result = CheckConstraintsReader.GroupRows(rows);

        Assert.Equal(2, result.Count);

        var people = Assert.Contains(("dbo", "People"), result);
        var ckPeople = Assert.Single(people);
        Assert.Equal("CK_People_Age",   ckPeople.Name);
        Assert.Equal("([Age] >= (0))",  ckPeople.Expression);

        var orders = Assert.Contains(("dbo", "Orders"), result);
        var ckOrders = Assert.Single(orders);
        Assert.Equal("CK_Orders_Total",  ckOrders.Name);
        Assert.Equal("([Total] > (0))",  ckOrders.Expression);
    }
}
