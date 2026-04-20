using ApiSmith.Core.Model;

namespace ApiSmith.UnitTests.Core;

public sealed class CheckConstraintTests
{
    [Fact]
    public void Record_equality_is_structural()
    {
        var a = new CheckConstraint("ck_x", "([x] > 0)");
        var b = new CheckConstraint("ck_x", "([x] > 0)");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_expressions_are_unequal()
    {
        var a = new CheckConstraint("ck_x", "([x] > 0)");
        var b = new CheckConstraint("ck_x", "([x] >= 0)");
        Assert.NotEqual(a, b);
    }
}
