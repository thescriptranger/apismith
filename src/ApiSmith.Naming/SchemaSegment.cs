namespace ApiSmith.Naming;

public static class SchemaSegment
{
    /// <summary>Schema name to PascalCase folder/namespace segment.</summary>
    public static string ToPascal(string schema) => Casing.ToPascal(schema);
}
