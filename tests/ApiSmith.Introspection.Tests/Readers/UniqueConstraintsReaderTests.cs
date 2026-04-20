using ApiSmith.Introspection.Readers;

namespace ApiSmith.Introspection.Tests.Readers;

public sealed class UniqueConstraintsReaderTests
{
    [Fact]
    public void Empty_input_maps_to_empty_dictionary()
    {
        var result = UniqueConstraintsReader.GroupRows(
            System.Array.Empty<(string, string, string, string, int)>());

        Assert.Empty(result);
    }

    [Fact]
    public void Single_constraint_with_two_columns_preserves_key_ordinal_order()
    {
        // reverse order to prove the sort happens
        var rows = new[]
        {
            ("dbo", "Users", "UQ_Users_Email_Tenant", "TenantId", 2),
            ("dbo", "Users", "UQ_Users_Email_Tenant", "Email",    1),
        };

        var result = UniqueConstraintsReader.GroupRows(rows);

        var kvp = Assert.Single(result);
        Assert.Equal(("dbo", "Users"), kvp.Key);
        var uc = Assert.Single(kvp.Value);
        Assert.Equal("UQ_Users_Email_Tenant", uc.Name);
        Assert.Equal(new[] { "Email", "TenantId" }, uc.Columns);
    }

    [Fact]
    public void Two_constraints_on_same_table_sorted_by_name_ordinal()
    {
        // reverse order to prove the sort happens
        var rows = new[]
        {
            ("dbo", "Products", "UQ_B_Products_Code", "Code", 1),
            ("dbo", "Products", "UQ_A_Products_Sku",  "Sku",  1),
        };

        var result = UniqueConstraintsReader.GroupRows(rows);

        var kvp = Assert.Single(result);
        Assert.Equal(("dbo", "Products"), kvp.Key);
        Assert.Collection(kvp.Value,
            uc =>
            {
                Assert.Equal("UQ_A_Products_Sku", uc.Name);
                Assert.Equal(new[] { "Sku" }, uc.Columns);
            },
            uc =>
            {
                Assert.Equal("UQ_B_Products_Code", uc.Name);
                Assert.Equal(new[] { "Code" }, uc.Columns);
            });
    }

    [Fact]
    public void Constraints_on_two_tables_yield_two_entries()
    {
        var rows = new[]
        {
            ("dbo",   "Users",    "UQ_Users_Email",    "Email",   1),
            ("sales", "Invoices", "UQ_Invoices_Number", "Number", 1),
        };

        var result = UniqueConstraintsReader.GroupRows(rows);

        Assert.Equal(2, result.Count);

        Assert.True(result.TryGetValue(("dbo", "Users"), out var usersUcs));
        var usersUc = Assert.Single(usersUcs!);
        Assert.Equal("UQ_Users_Email", usersUc.Name);
        Assert.Equal(new[] { "Email" }, usersUc.Columns);

        Assert.True(result.TryGetValue(("sales", "Invoices"), out var invoicesUcs));
        var invoicesUc = Assert.Single(invoicesUcs!);
        Assert.Equal("UQ_Invoices_Number", invoicesUc.Name);
        Assert.Equal(new[] { "Number" }, invoicesUc.Columns);
    }
}
