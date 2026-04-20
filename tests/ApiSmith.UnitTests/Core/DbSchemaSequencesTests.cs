using ApiSmith.Core.Model;

namespace ApiSmith.UnitTests.Core;

public sealed class DbSchemaSequencesTests
{
    [Fact]
    public void Default_schema_has_empty_sequences()
    {
        var s = DbSchema.Create("dbo", System.Array.Empty<Table>());
        Assert.Empty(s.Sequences);
    }

    [Fact]
    public void Sequences_are_sorted_by_name_ordinal()
    {
        var s = DbSchema.Create(
            name: "dbo",
            tables: System.Array.Empty<Table>(),
            sequences: new[]
            {
                new Sequence("dbo", "seq_b", "bigint", 1L, 1L, null, null, false),
                new Sequence("dbo", "seq_a", "int",    1L, 1L, null, null, false),
            });
        Assert.Equal(new[] { "seq_a", "seq_b" }, s.Sequences.Select(x => x.Name).ToArray());
    }
}
