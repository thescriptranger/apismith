namespace ApiSmith.Introspection.Readers;

/// <summary>Built-in SQL Server schemas excluded from introspection by default.</summary>
internal static class SystemSchemas
{
    public static readonly HashSet<string> Names = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "sys", "INFORMATION_SCHEMA", "guest",
        "db_owner", "db_accessadmin", "db_securityadmin", "db_ddladmin",
        "db_backupoperator", "db_datareader", "db_datawriter",
        "db_denydatareader", "db_denydatawriter",
    };

    public static string FilterClause(string schemaColumn) =>
        $"{schemaColumn} NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest', 'db_owner', " +
        "'db_accessadmin', 'db_securityadmin', 'db_ddladmin', 'db_backupoperator', " +
        "'db_datareader', 'db_datawriter', 'db_denydatareader', 'db_denydatawriter')";
}
