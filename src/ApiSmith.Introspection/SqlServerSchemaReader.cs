using ApiSmith.Core.Model;
using ApiSmith.Introspection.Readers;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection;

/// <summary>Facade that runs every reader and assembles the <see cref="SchemaGraph"/>.</summary>
public sealed class SqlServerSchemaReader
{
    public readonly record struct ConnectionValidation(bool IsValid, string? ErrorMessage);

    /// <summary>Pre-flight probe with a 5s timeout. Connection failures return a result; cancellation propagates.</summary>
    public static async Task<ConnectionValidation> ValidateAsync(
        string connectionString,
        CancellationToken ct = default)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = 5,
            };
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            return new ConnectionValidation(IsValid: true, ErrorMessage: null);
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            return new ConnectionValidation(IsValid: false, ErrorMessage: ex.Message);
        }
    }

    public async Task<SchemaGraph> ReadAsync(
        string connectionString,
        IReadOnlyCollection<string>? schemaFilter = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var tables      = await new TablesReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var foreignKeys = await new ForeignKeysReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var views       = await new ViewsReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var sprocs      = await new StoredProceduresReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var functions   = await new FunctionsReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var uniques     = await new UniqueConstraintsReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var checks      = await new CheckConstraintsReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var indexes     = await new IndexesReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var sequences   = await new SequencesReader().ReadAsync(conn, schemaFilter, ct).ConfigureAwait(false);

        return BuildGraph(tables, foreignKeys, views, sprocs, functions, uniques, checks, indexes, sequences);
    }

    /// <summary>Assembles the graph from pre-loaded reader results; split out so tests can skip the DB.</summary>
    public static SchemaGraph BuildGraph(
        IReadOnlyList<Table> tables,
        IReadOnlyList<ForeignKey> foreignKeys,
        IReadOnlyList<View> views,
        IReadOnlyList<StoredProcedure> sprocs,
        IReadOnlyList<DbFunction> functions,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<UniqueConstraint>>? uniques = null,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<CheckConstraint>>? checks = null,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<TableIndex>>? indexes = null,
        IReadOnlyList<Sequence>? sequences = null)
    {
        var fksByTable = foreignKeys
            .GroupBy(f => (f.FromSchema, f.FromTable))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ForeignKey>)g.ToList());

        var enrichedTables = tables
            .Select(t =>
            {
                fksByTable.TryGetValue((t.Schema, t.Name), out var tableFks);
                tableFks ??= System.Array.Empty<ForeignKey>();

                // Prefer dedicated-reader output; fall back to whatever Table carried.
                IEnumerable<UniqueConstraint> tableUniques = t.UniqueConstraints;
                if (uniques is not null && uniques.TryGetValue((t.Schema, t.Name), out var u))
                {
                    tableUniques = u;
                }

                IEnumerable<CheckConstraint> tableChecks = t.CheckConstraints;
                if (checks is not null && checks.TryGetValue((t.Schema, t.Name), out var c))
                {
                    tableChecks = c;
                }

                IEnumerable<TableIndex> tableIndexes = t.Indexes;
                if (indexes is not null && indexes.TryGetValue((t.Schema, t.Name), out var i))
                {
                    tableIndexes = i;
                }

                var isJoin = JoinTableDetector.IsJoinTable(t, tableFks);
                return Table.Create(
                    t.Schema, t.Name, t.Columns, t.PrimaryKey,
                    foreignKeys: tableFks,
                    uniqueConstraints: tableUniques,
                    indexes: tableIndexes,
                    checkConstraints: tableChecks,
                    isJoinTable: isJoin);
            })
            .ToList();

        var bySchema = enrichedTables
            .GroupBy(t => t.Schema)
            .ToDictionary(g => g.Key, g => g.ToList());

        var viewsBySchema = views
            .GroupBy(v => v.Schema)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sprocsBySchema = sprocs
            .GroupBy(p => p.Schema)
            .ToDictionary(g => g.Key, g => g.ToList());

        var functionsBySchema = functions
            .GroupBy(f => f.Schema)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sequencesBySchema = (sequences ?? System.Array.Empty<Sequence>())
            .GroupBy(s => s.Schema)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allSchemaNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var s in bySchema.Keys)          { allSchemaNames.Add(s); }
        foreach (var s in viewsBySchema.Keys)     { allSchemaNames.Add(s); }
        foreach (var s in sprocsBySchema.Keys)    { allSchemaNames.Add(s); }
        foreach (var s in functionsBySchema.Keys) { allSchemaNames.Add(s); }
        foreach (var s in sequencesBySchema.Keys) { allSchemaNames.Add(s); }

        var dbSchemas = allSchemaNames
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .Select(name => DbSchema.Create(
                name,
                bySchema.GetValueOrDefault(name, new List<Table>()),
                viewsBySchema.GetValueOrDefault(name, new List<View>()),
                sprocsBySchema.GetValueOrDefault(name, new List<StoredProcedure>()),
                functionsBySchema.GetValueOrDefault(name, new List<DbFunction>()),
                sequencesBySchema.GetValueOrDefault(name, new List<Sequence>())))
            .ToList();

        return SchemaGraph.Create(dbSchemas);
    }
}
