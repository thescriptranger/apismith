using ApiSmith.Core.Model;

namespace ApiSmith.Introspection.Readers;

/// <summary>Detects pure M:N join tables: two FKs whose columns equal the PK, audit columns tolerated. Emitted as EF skip navigations.</summary>
internal static class JoinTableDetector
{
    private static readonly HashSet<string> TolerableColumns = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "created_at", "created_by", "created_on",
        "modified_at", "modified_by", "modified_on",
        "updated_at", "updated_by", "updated_on",
        "timestamp", "rowversion",
    };

    public static bool IsJoinTable(Table table, IReadOnlyList<ForeignKey> foreignKeysForTable)
    {
        if (foreignKeysForTable.Count != 2 || table.PrimaryKey is null)
        {
            return false;
        }

        var pkSet = new HashSet<string>(table.PrimaryKey.Columns, System.StringComparer.OrdinalIgnoreCase);
        var fkColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var fk in foreignKeysForTable)
        {
            foreach (var col in fk.FromColumns)
            {
                fkColumns.Add(col);
            }
        }

        if (!pkSet.SetEquals(fkColumns))
        {
            return false;
        }

        foreach (var col in table.Columns)
        {
            if (pkSet.Contains(col.Name))
            {
                continue;
            }

            if (TolerableColumns.Contains(col.Name))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
