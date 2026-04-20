using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>
/// Emits one repository class per entity. Standard CRUD via Dapper + parameterized SQL
/// against SQL Server. Uses the entity's identity-column primary key when one exists;
/// falls back to list-only for keyless tables.
/// </summary>
public static class DapperRepositoryEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var repoNs = layout.RepositoryNamespace(config);
        var dataNs = layout.DataNamespace(config);
        var entityNs = layout.EntityNamespace(config, table.Schema);
        var entity = table.EntityName;

        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Dapper;");
        if (dataNs != repoNs)
        {
            sb.AppendLine($"using {dataNs};");
        }
        if (entityNs != repoNs)
        {
            sb.AppendLine($"using {entityNs};");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {repoNs};");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {entity}Repository");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly IDbConnectionFactory _connections;");
        sb.AppendLine();
        sb.AppendLine($"    public {entity}Repository(IDbConnectionFactory connections)");
        sb.AppendLine("    {");
        sb.AppendLine("        _connections = connections;");
        sb.AppendLine("    }");

        var selectCols = string.Join(", ", table.Columns.Select(c => $"[{c.DbName}] AS {c.PropertyName}"));
        var fullTable = $"[{table.Schema}].[{table.DbTableName}]";

        sb.AppendLine();
        sb.AppendLine($"    public async Task<IReadOnlyList<{entity}>> ListAsync(CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var conn = await _connections.OpenAsync(ct).ConfigureAwait(false);");
        sb.AppendLine($"        var rows = await conn.QueryAsync<{entity}>(\"SELECT {selectCols} FROM {fullTable}\").ConfigureAwait(false);");
        sb.AppendLine("        return rows.AsList();");
        sb.AppendLine("    }");

        if (table.PrimaryKey is null)
        {
            sb.AppendLine("}");
            return new EmittedFile(layout.RepositoryPath(config, entity), sb.ToString());
        }

        var pk = table.PrimaryKey;
        var pkCol = table.Columns.First(c => c.PropertyName == pk.PropertyName);
        var insertCols = table.Columns.Where(c => !c.IsIdentity).ToList();
        var insertColList = string.Join(", ", insertCols.Select(c => $"[{c.DbName}]"));
        var insertParamList = string.Join(", ", insertCols.Select(c => $"@{c.PropertyName}"));
        var updateSet = string.Join(", ", insertCols.Select(c => $"[{c.DbName}] = @{c.PropertyName}"));

        sb.AppendLine();
        sb.AppendLine($"    public async Task<{entity}?> GetByIdAsync({pk.ClrTypeName} id, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var conn = await _connections.OpenAsync(ct).ConfigureAwait(false);");
        sb.AppendLine($"        return await conn.QuerySingleOrDefaultAsync<{entity}>(\"SELECT {selectCols} FROM {fullTable} WHERE [{pkCol.DbName}] = @id\", new {{ id }}).ConfigureAwait(false);");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    public async Task<{entity}> CreateAsync({entity} entity, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var conn = await _connections.OpenAsync(ct).ConfigureAwait(false);");

        if (pkCol.IsIdentity)
        {
            sb.AppendLine($"        var sql = \"INSERT INTO {fullTable} ({insertColList}) OUTPUT INSERTED.[{pkCol.DbName}] VALUES ({insertParamList})\";");
            sb.AppendLine($"        var newId = await conn.ExecuteScalarAsync<{pk.ClrTypeName}>(sql, entity).ConfigureAwait(false);");
            sb.AppendLine($"        entity.{pk.PropertyName} = newId;");
        }
        else
        {
            sb.AppendLine($"        var sql = \"INSERT INTO {fullTable} ({insertColList}) VALUES ({insertParamList})\";");
            sb.AppendLine("        await conn.ExecuteAsync(sql, entity).ConfigureAwait(false);");
        }

        sb.AppendLine("        return entity;");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    public async Task<bool> UpdateAsync({entity} entity, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var conn = await _connections.OpenAsync(ct).ConfigureAwait(false);");
        sb.AppendLine($"        var rows = await conn.ExecuteAsync(\"UPDATE {fullTable} SET {updateSet} WHERE [{pkCol.DbName}] = @{pk.PropertyName}\", entity).ConfigureAwait(false);");
        sb.AppendLine("        return rows > 0;");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    public async Task<bool> DeleteAsync({pk.ClrTypeName} id, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var conn = await _connections.OpenAsync(ct).ConfigureAwait(false);");
        sb.AppendLine($"        var rows = await conn.ExecuteAsync(\"DELETE FROM {fullTable} WHERE [{pkCol.DbName}] = @id\", new {{ id }}).ConfigureAwait(false);");
        sb.AppendLine("        return rows > 0;");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return new EmittedFile(layout.RepositoryPath(config, entity), sb.ToString());
    }
}
