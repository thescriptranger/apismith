namespace ApiSmith.Introspection.TypeMapping;

/// <summary>SQL Server type name to C# type name. Caller adds <c>?</c> for nullable columns.</summary>
public static class SqlTypeMapper
{
    private static readonly Dictionary<string, string> Map = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["bigint"]           = "long",
        ["binary"]           = "byte[]",
        ["bit"]              = "bool",
        ["char"]             = "string",
        ["date"]             = "System.DateOnly",
        ["datetime"]         = "System.DateTime",
        ["datetime2"]        = "System.DateTime",
        ["datetimeoffset"]   = "System.DateTimeOffset",
        ["decimal"]          = "decimal",
        ["float"]            = "double",
        ["image"]            = "byte[]",
        ["int"]              = "int",
        ["money"]            = "decimal",
        ["nchar"]            = "string",
        ["ntext"]            = "string",
        ["numeric"]          = "decimal",
        ["nvarchar"]         = "string",
        ["real"]             = "float",
        ["rowversion"]       = "byte[]",
        ["smalldatetime"]    = "System.DateTime",
        ["smallint"]         = "short",
        ["smallmoney"]       = "decimal",
        ["sql_variant"]      = "object",
        ["text"]             = "string",
        ["time"]             = "System.TimeOnly",
        ["timestamp"]        = "byte[]",
        ["tinyint"]          = "byte",
        ["uniqueidentifier"] = "System.Guid",
        ["varbinary"]        = "byte[]",
        ["varchar"]          = "string",
        ["xml"]              = "string",
    };

    public static string ToClrTypeName(string sqlType) =>
        Map.TryGetValue(sqlType, out var clr) ? clr : "object";

    public static bool IsReferenceType(string clrTypeName) =>
        clrTypeName is "string" or "byte[]" or "object";
}
