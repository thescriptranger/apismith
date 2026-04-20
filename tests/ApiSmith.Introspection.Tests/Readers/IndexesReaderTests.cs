using ApiSmith.Introspection.Readers;

namespace ApiSmith.Introspection.Tests.Readers;

public sealed class IndexesReaderTests
{
    [Fact]
    public void Empty_rows_produce_empty_dictionary()
    {
        var result = IndexesReader.GroupRows(System.Array.Empty<IndexesReader.Row>());

        Assert.Empty(result);
    }

    [Fact]
    public void Single_non_unique_composite_index_groups_columns_in_key_order()
    {
        var rows = new[]
        {
            new IndexesReader.Row("dbo", "Foo", "IX_Foo", IsUnique: false, ColumnName: "colA", KeyOrdinal: 1),
            new IndexesReader.Row("dbo", "Foo", "IX_Foo", IsUnique: false, ColumnName: "colB", KeyOrdinal: 2),
        };

        var result = IndexesReader.GroupRows(rows);

        var indexes = Assert.Single(result);
        Assert.Equal(("dbo", "Foo"), indexes.Key);

        var index = Assert.Single(indexes.Value);
        Assert.Equal("IX_Foo", index.Name);
        Assert.False(index.IsUnique);
        Assert.Equal(new[] { "colA", "colB" }, index.Columns);
    }

    [Fact]
    public void Columns_are_sorted_by_key_ordinal_regardless_of_row_order()
    {
        // reverse order to prove the sort happens
        var rows = new[]
        {
            new IndexesReader.Row("dbo", "Foo", "IX_Foo", IsUnique: false, ColumnName: "colB", KeyOrdinal: 2),
            new IndexesReader.Row("dbo", "Foo", "IX_Foo", IsUnique: false, ColumnName: "colA", KeyOrdinal: 1),
        };

        var result = IndexesReader.GroupRows(rows);

        var index = Assert.Single(result.Single().Value);
        Assert.Equal(new[] { "colA", "colB" }, index.Columns);
    }

    [Fact]
    public void Unique_non_constraint_index_flags_IsUnique_true()
    {
        var rows = new[]
        {
            new IndexesReader.Row("dbo", "Foo", "UX_Foo_email", IsUnique: true, ColumnName: "email", KeyOrdinal: 1),
        };

        var result = IndexesReader.GroupRows(rows);

        var index = Assert.Single(result.Single().Value);
        Assert.Equal("UX_Foo_email", index.Name);
        Assert.True(index.IsUnique);
        Assert.Equal(new[] { "email" }, index.Columns);
    }

    [Fact]
    public void Two_indexes_on_one_table_are_sorted_alphabetically()
    {
        // reverse order to prove the sort happens
        var rows = new[]
        {
            new IndexesReader.Row("dbo", "Foo", "IX_zeta",  IsUnique: false, ColumnName: "z", KeyOrdinal: 1),
            new IndexesReader.Row("dbo", "Foo", "IX_alpha", IsUnique: false, ColumnName: "a", KeyOrdinal: 1),
        };

        var result = IndexesReader.GroupRows(rows);

        var kvp = Assert.Single(result);
        var names = kvp.Value.Select(i => i.Name).ToArray();
        Assert.Equal(new[] { "IX_alpha", "IX_zeta" }, names);
    }

    [Fact]
    public void Indexes_across_two_tables_produce_two_dictionary_entries()
    {
        var rows = new[]
        {
            new IndexesReader.Row("dbo", "Foo", "IX_Foo", IsUnique: false, ColumnName: "a", KeyOrdinal: 1),
            new IndexesReader.Row("dbo", "Bar", "IX_Bar", IsUnique: false, ColumnName: "b", KeyOrdinal: 1),
        };

        var result = IndexesReader.GroupRows(rows);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(("dbo", "Foo")));
        Assert.True(result.ContainsKey(("dbo", "Bar")));

        Assert.Equal("IX_Foo", result[("dbo", "Foo")].Single().Name);
        Assert.Equal("IX_Bar", result[("dbo", "Bar")].Single().Name);
    }
}
