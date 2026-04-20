namespace ApiSmith.Introspection.Readers;

internal static class SchemaFilter
{
    public static bool Accepts(IReadOnlyCollection<string>? filter, string schemaName)
    {
        if (filter is null || filter.Count == 0)
        {
            return !SystemSchemas.Names.Contains(schemaName);
        }

        return filter.Contains(schemaName);
    }
}
