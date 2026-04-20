namespace ApiSmith.Naming;

/// <summary>FK metadata to C# navigation property names.</summary>
public static class NavigationNamer
{
    /// <summary>Singular nav on source → target. Strips "Id" suffix; falls back to target entity name. Caller handles disambiguation.</summary>
    public static string ReferenceName(string fkColumnName, string targetEntityName)
    {
        var pascalCol = Casing.ToPascal(fkColumnName);
        if (pascalCol.EndsWith("Id", System.StringComparison.Ordinal) && pascalCol.Length > 2)
        {
            var trimmed = pascalCol[..^2];
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }
        return targetEntityName;
    }

    /// <summary>Plural collection nav on target → source (e.g. <c>User.Posts</c>).</summary>
    public static string CollectionName(string sourceEntityName) =>
        Pluralizer.Pluralize(sourceEntityName);
}
