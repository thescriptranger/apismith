using ApiSmith.Introspection.Readers;

namespace ApiSmith.Introspection.Tests.Readers;

public sealed class SequencesReaderTests
{
    [Fact]
    public void Empty_input_maps_to_empty_list()
    {
        var result = SequencesReader.MapRows(System.Array.Empty<SequencesReader.Row>());

        Assert.Empty(result);
    }

    [Fact]
    public void Single_sequence_with_standard_values_maps_verbatim()
    {
        var rows = new[]
        {
            new SequencesReader.Row(
                SchemaName: "dbo",
                SequenceName: "seq_orders",
                TypeName: "bigint",
                StartValue: 1L,
                Increment: 1L,
                MinValue: 1L,
                MaxValue: long.MaxValue,
                Cycle: false),
        };

        var result = SequencesReader.MapRows(rows);

        var seq = Assert.Single(result);
        Assert.Equal("dbo", seq.Schema);
        Assert.Equal("seq_orders", seq.Name);
        Assert.Equal("bigint", seq.TypeName);
        Assert.Equal(1L, seq.StartValue);
        Assert.Equal(1L, seq.Increment);
        Assert.Equal(1L, seq.MinValue);
        Assert.Equal(long.MaxValue, seq.MaxValue);
        Assert.False(seq.Cycle);
    }

    [Fact]
    public void Custom_start_increment_and_cycle_are_preserved()
    {
        var rows = new[]
        {
            new SequencesReader.Row(
                SchemaName: "sales",
                SequenceName: "seq_invoice",
                TypeName: "int",
                StartValue: 1000L,
                Increment: 5L,
                MinValue: 1000L,
                MaxValue: 9999L,
                Cycle: true),
        };

        var result = SequencesReader.MapRows(rows);

        var seq = Assert.Single(result);
        Assert.Equal("sales", seq.Schema);
        Assert.Equal("seq_invoice", seq.Name);
        Assert.Equal("int", seq.TypeName);
        Assert.Equal(1000L, seq.StartValue);
        Assert.Equal(5L, seq.Increment);
        Assert.Equal(1000L, seq.MinValue);
        Assert.Equal(9999L, seq.MaxValue);
        Assert.True(seq.Cycle);
    }

    [Fact]
    public void Two_sequences_across_two_schemas_sorted_by_schema_then_name_ordinal()
    {
        // reverse order to prove the sort happens
        var rows = new List<SequencesReader.Row>
        {
            new(
                SchemaName: "sales",
                SequenceName: "seq_b",
                TypeName: "bigint",
                StartValue: 1L,
                Increment: 1L,
                MinValue: 1L,
                MaxValue: long.MaxValue,
                Cycle: false),
            new(
                SchemaName: "dbo",
                SequenceName: "seq_a",
                TypeName: "bigint",
                StartValue: 1L,
                Increment: 1L,
                MinValue: 1L,
                MaxValue: long.MaxValue,
                Cycle: false),
        };

        var result = SequencesReader.MapRows(rows);

        Assert.Collection(result,
            s =>
            {
                Assert.Equal("dbo", s.Schema);
                Assert.Equal("seq_a", s.Name);
            },
            s =>
            {
                Assert.Equal("sales", s.Schema);
                Assert.Equal("seq_b", s.Name);
            });
    }
}
