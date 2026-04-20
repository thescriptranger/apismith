using ApiSmith.Naming;

namespace ApiSmith.UnitTests.Naming;

public sealed class NavigationNamerTests
{
    [Theory]
    [InlineData("user_id", "User", "User")]
    [InlineData("created_by_user_id", "User", "CreatedByUser")]
    [InlineData("parent_id", "Category", "Parent")]
    [InlineData("owner", "User", "User")]          // no Id suffix → fall back to target entity name
    public void ReferenceName_drops_id_suffix_when_natural(string fkColumn, string target, string expected)
    {
        Assert.Equal(expected, NavigationNamer.ReferenceName(fkColumn, target));
    }

    [Theory]
    [InlineData("Post", "Posts")]
    [InlineData("Category", "Categories")]
    public void CollectionName_pluralizes(string source, string expected)
    {
        Assert.Equal(expected, NavigationNamer.CollectionName(source));
    }
}
