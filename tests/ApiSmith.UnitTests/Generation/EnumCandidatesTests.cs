using ApiSmith.Generation;

namespace ApiSmith.UnitTests.Generation;

public sealed class EnumCandidatesTests
{
    [Fact]
    public void Translates_simple_in_list()
    {
        var result = EnumCandidates.TryParseInList("([Status] IN ('draft','published','archived'))");
        Assert.NotNull(result);
        Assert.Equal("Status", result!.Column);
        Assert.Equal(new[] { "draft", "published", "archived" }, result.Values);
    }

    [Fact]
    public void Translates_in_list_without_outer_parens()
    {
        var result = EnumCandidates.TryParseInList("[Status] IN ('a','b')");
        Assert.NotNull(result);
        Assert.Equal("Status", result!.Column);
    }

    [Fact]
    public void Translates_in_list_case_insensitive_keyword()
    {
        var result = EnumCandidates.TryParseInList("([Status] in ('a','b'))");
        Assert.NotNull(result);
    }

    [Fact]
    public void Ignores_non_in_expressions()
    {
        Assert.Null(EnumCandidates.TryParseInList("([Age] >= 0)"));
        Assert.Null(EnumCandidates.TryParseInList("([Name] IS NOT NULL)"));
        Assert.Null(EnumCandidates.TryParseInList("(LEN([Email]) > 0)"));
    }

    [Fact]
    public void Case_colliding_values_return_null()
    {
        // Pascal-casing 'draft' and 'DRAFT' would produce the same enum member name.
        Assert.Null(EnumCandidates.TryParseInList("([Status] IN ('draft','DRAFT'))"));
    }

    [Fact]
    public void Handles_column_without_brackets()
    {
        var result = EnumCandidates.TryParseInList("(Status IN ('a','b'))");
        Assert.NotNull(result);
        Assert.Equal("Status", result!.Column);
    }
}
