using ApiSmith.Generation.Validation;

namespace ApiSmith.UnitTests.Generation;

public sealed class CheckConstraintTranslatorTests
{
    [Theory]
    [InlineData("([Age] >= 0)",            "Age",   ">=", 0L)]
    [InlineData("([Age] >= (0))",          "Age",   ">=", 0L)]
    [InlineData("([Amount]>(0))",          "Amount", ">", 0L)]
    [InlineData("([Price] <= 9999)",       "Price", "<=", 9999L)]
    [InlineData("([Total] < (1000000))",   "Total",  "<", 1000000L)]
    [InlineData("[Age]>=0",                "Age",   ">=", 0L)]
    public void Parses_simple_numeric_comparison(string expr, string col, string op, long value)
    {
        var rule = CheckConstraintTranslator.TryTranslate(expr);
        var c = Assert.IsType<ComparisonRule>(rule);
        Assert.Equal(col, c.Column);
        Assert.Equal(op, c.Operator);
        Assert.Equal(value, c.LiteralValue);
    }

    [Theory]
    [InlineData("([Age] BETWEEN 0 AND 120)",     "Age",   0L, 120L)]
    [InlineData("([Age] between (0) and (120))", "Age",   0L, 120L)]
    public void Parses_between(string expr, string col, long lo, long hi)
    {
        var rule = CheckConstraintTranslator.TryTranslate(expr);
        var b = Assert.IsType<BetweenRule>(rule);
        Assert.Equal(col, b.Column);
        Assert.Equal(lo, b.LowerInclusive);
        Assert.Equal(hi, b.UpperInclusive);
    }

    [Theory]
    [InlineData("([Status] IN ('draft','published','archived'))")]
    [InlineData("([State]='on' OR [State]='off')")]
    [InlineData("([Name] IS NOT NULL)")]
    [InlineData("(LEN([Email]) > 0)")]
    [InlineData("([A] > [B])")]
    public void Unsupported_expressions_return_null_for_todo_emission(string expr)
    {
        Assert.Null(CheckConstraintTranslator.TryTranslate(expr));
    }

    [Fact]
    public void Handles_negative_literals()
    {
        var rule = CheckConstraintTranslator.TryTranslate("([Delta] > -5)");
        var c = Assert.IsType<ComparisonRule>(rule);
        Assert.Equal("Delta", c.Column);
        Assert.Equal(">", c.Operator);
        Assert.Equal(-5L, c.LiteralValue);
    }
}
