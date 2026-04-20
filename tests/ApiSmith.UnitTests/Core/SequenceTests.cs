using ApiSmith.Core.Model;

namespace ApiSmith.UnitTests.Core;

public sealed class SequenceTests
{
    [Fact]
    public void Record_equality_is_structural()
    {
        var a = new Sequence("dbo", "seq_ids", "bigint", 1L, 1L, MinValue: 1L, MaxValue: 9223372036854775807L, Cycle: false);
        var b = new Sequence("dbo", "seq_ids", "bigint", 1L, 1L, MinValue: 1L, MaxValue: 9223372036854775807L, Cycle: false);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Null_min_max_supported()
    {
        var s = new Sequence("dbo", "seq_unbounded", "int", 1L, 1L, null, null, false);
        Assert.Null(s.MinValue);
        Assert.Null(s.MaxValue);
    }
}
