using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Emits one <c>XxxEndpoints</c> class per entity with a <c>MapXxxEndpoints</c> extension; works with EF Core or Dapper via DI.</summary>
public static class MinimalApiEndpointEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var ns = layout.EndpointNamespace(config);
        var dataNs = config.DataAccess is DataAccessStyle.EfCore
            ? layout.DataNamespace(config)
            : layout.RepositoryNamespace(config);
        var entityNs = layout.EntityNamespace(config, table.Schema);
        var dtoNs = layout.DtoNamespace(config, table.Schema);
        var mapperNs = layout.MapperNamespace(config, table.Schema);
        var validatorNs = layout.ValidatorNamespace(config, table.Schema);
        var entity = table.EntityName;
        var collection = table.CollectionName;
        // Only the DbContext property name is disambiguated for collisions; types/routes stay bare. See DbSetNaming.
        var dbset = DbSetNaming.PropertyName(table, collidedEntityNames);
        var route = table.RouteSegment;
        var crud = config.Crud;

        var usings = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "Microsoft.AspNetCore.Builder",
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Routing",
        };
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            usings.Add("Microsoft.EntityFrameworkCore");
        }
        usings.Add(dataNs);
        usings.Add(dtoNs);
        usings.Add(entityNs);
        usings.Add(mapperNs);
        usings.Add(validatorNs);
        // IValidator<TDto> lives in the unsegmented validator core namespace; match ValidationResult.cs.
        usings.Add(layout.ValidatorCoreNamespace(config));
        if (config.ApiVersion == ApiVersion.V2)
        {
            usings.Add("System.Collections.Immutable");
            usings.Add(layout.SharedErrorsNamespace(config));
        }

        var sb = new StringBuilder();
        foreach (var u in usings.OrderBy(u => u, System.StringComparer.Ordinal))
        {
            sb.AppendLine($"using {u};");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public static class {collection}Endpoints");
        sb.AppendLine("{");
        sb.AppendLine($"    public static IEndpointRouteBuilder Map{collection}Endpoints(this IEndpointRouteBuilder app)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var group = app.MapGroup(\"{VersioningEmitter.MinimalApiGroupPrefix(config)}/{route}\");");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("        group.RequireAuthorization();");
        }
        sb.AppendLine();

        var listRepoType = config.EmitRepositoryInterfaces ? $"I{entity}Repository" : $"{entity}Repository";
        if (table.PrimaryKey is null)
        {
            sb.AppendLine("        // No primary key discovered — read-only list endpoint only.");
            EmitListHandler(sb, config, table, dbset, listRepoType);
            sb.AppendLine();
            sb.AppendLine("        return app;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return new EmittedFile(layout.MinimalApiEndpointPath(config, collection), sb.ToString());
        }

        var pk = table.PrimaryKey;
        var dbCtx = $"{config.ProjectName}DbContext";
        var repoType = config.EmitRepositoryInterfaces ? $"I{entity}Repository" : $"{entity}Repository";

        if ((crud & CrudOperations.GetList) != 0) EmitListHandler(sb, config, table, dbset, listRepoType);
        if ((crud & CrudOperations.GetById) != 0) EmitGetByIdHandler(sb, config, table, pk, dbCtx, repoType, dbset);
        if ((crud & CrudOperations.Post) != 0)    EmitCreateHandler(sb, config, table, pk, dbCtx, repoType, dbset);
        if ((crud & CrudOperations.Put) != 0)     EmitUpdateHandler(sb, config, table, pk, dbCtx, repoType, "MapPut", "Update", dbset);
        if ((crud & CrudOperations.Patch) != 0)   EmitUpdateHandler(sb, config, table, pk, dbCtx, repoType, "MapPatch", "Patch", dbset);
        if ((crud & CrudOperations.Delete) != 0)  EmitDeleteHandler(sb, config, table, pk, dbCtx, repoType, dbset);

        sb.AppendLine();
        sb.AppendLine("        return app;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new EmittedFile(layout.MinimalApiEndpointPath(config, collection), sb.ToString());
    }

    private static void EmitListHandler(StringBuilder sb, ApiSmithConfig config, NamedTable table, string dbset, string repoType)
    {
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            sb.AppendLine($"        group.MapGet(\"/\", async ({config.ProjectName}DbContext db, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            // Extension point: chain filter/page/sort onto this IQueryable<{table.EntityName}>.");
            sb.AppendLine($"            IQueryable<{table.EntityName}> query = db.{dbset}.AsNoTracking();");
            sb.AppendLine("            var items = await query.ToListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("            return Results.Ok(items.Select(e => e.ToDto()).ToList());");
            sb.AppendLine("        });");
        }
        else
        {
            sb.AppendLine($"        group.MapGet(\"/\", async ({repoType} repo, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var items = await repo.ListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("            return Results.Ok(items.Select(e => e.ToDto()).ToList());");
            sb.AppendLine("        });");
        }
    }

    private static void EmitGetByIdHandler(StringBuilder sb, ApiSmithConfig config, NamedTable table, NamedColumn pk, string dbCtx, string repoType, string dbset)
    {
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            sb.AppendLine();
            sb.AppendLine($"        group.MapGet(\"/{{id}}\", async ({pk.ClrTypeName} id, {dbCtx} db, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var entity = await db.{dbset}.AsNoTracking()");
            sb.AppendLine($"                .FirstOrDefaultAsync(e => e.{pk.PropertyName} == id, ct).ConfigureAwait(false);");
            sb.AppendLine("            return entity is null ? Results.NotFound() : Results.Ok(entity.ToDto());");
            sb.AppendLine("        });");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"        group.MapGet(\"/{{id}}\", async ({pk.ClrTypeName} id, {repoType} repo, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var entity = await repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
            sb.AppendLine("            return entity is null ? Results.NotFound() : Results.Ok(entity.ToDto());");
            sb.AppendLine("        });");
        }
    }

    private static void EmitCreateHandler(StringBuilder sb, ApiSmithConfig config, NamedTable table, NamedColumn pk, string dbCtx, string repoType, string dbset)
    {
        sb.AppendLine();
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            sb.AppendLine($"        group.MapPost(\"/\", async (Create{table.EntityName}Dto dto, {dbCtx} db, IValidator<Create{table.EntityName}Dto> validator, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = validator.Validate(dto);");
            AppendBadRequestIfInvalid(sb, config, indent: "            ");
            sb.AppendLine();
            sb.AppendLine("            var entity = dto.ToEntity();");
            sb.AppendLine($"            db.{dbset}.Add(entity);");
            sb.AppendLine("            await db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine($"            return Results.Created($\"{VersioningEmitter.MinimalApiGroupPrefix(config)}/{table.RouteSegment}/{{entity.{pk.PropertyName}}}\", entity.ToDto());");
            sb.AppendLine("        });");
        }
        else
        {
            sb.AppendLine($"        group.MapPost(\"/\", async (Create{table.EntityName}Dto dto, {repoType} repo, IValidator<Create{table.EntityName}Dto> validator, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = validator.Validate(dto);");
            AppendBadRequestIfInvalid(sb, config, indent: "            ");
            sb.AppendLine();
            sb.AppendLine("            var entity = await repo.CreateAsync(dto.ToEntity(), ct).ConfigureAwait(false);");
            sb.AppendLine($"            return Results.Created($\"{VersioningEmitter.MinimalApiGroupPrefix(config)}/{table.RouteSegment}/{{entity.{pk.PropertyName}}}\", entity.ToDto());");
            sb.AppendLine("        });");
        }
    }

    private static void EmitUpdateHandler(StringBuilder sb, ApiSmithConfig config, NamedTable table, NamedColumn pk, string dbCtx, string repoType, string mapMethod, string label, string dbset)
    {
        sb.AppendLine();
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            sb.AppendLine($"        group.{mapMethod}(\"/{{id}}\", async ({pk.ClrTypeName} id, Update{table.EntityName}Dto dto, {dbCtx} db, IValidator<Update{table.EntityName}Dto> validator, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = validator.Validate(dto);");
            AppendBadRequestIfInvalid(sb, config, indent: "            ");
            sb.AppendLine();
            sb.AppendLine($"            var entity = await db.{dbset}.FindAsync(new object[] {{ id }}, ct).ConfigureAwait(false);");
            sb.AppendLine("            if (entity is null) { return Results.NotFound(); }");
            sb.AppendLine("            entity.UpdateFromDto(dto);");
            sb.AppendLine("            await db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("            return Results.NoContent();");
            sb.AppendLine("        });");
        }
        else
        {
            sb.AppendLine($"        group.{mapMethod}(\"/{{id}}\", async ({pk.ClrTypeName} id, Update{table.EntityName}Dto dto, {repoType} repo, IValidator<Update{table.EntityName}Dto> validator, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = validator.Validate(dto);");
            AppendBadRequestIfInvalid(sb, config, indent: "            ");
            sb.AppendLine();
            sb.AppendLine("            var entity = await repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
            sb.AppendLine("            if (entity is null) { return Results.NotFound(); }");
            sb.AppendLine("            entity.UpdateFromDto(dto);");
            sb.AppendLine("            await repo.UpdateAsync(entity, ct).ConfigureAwait(false);");
            sb.AppendLine("            return Results.NoContent();");
            sb.AppendLine("        });");
        }
    }

    private static void AppendBadRequestIfInvalid(StringBuilder sb, ApiSmithConfig config, string indent)
    {
        if (config.ApiVersion == ApiVersion.V2)
        {
            sb.AppendLine($"{indent}if (!validation.IsValid) {{ return Results.BadRequest(new ApiProblem(\"Validation failed\", 400, \"https://apismith.dev/problems/validation\", validation.Errors.ToImmutableArray())); }}");
        }
        else
        {
            sb.AppendLine($"{indent}if (!validation.IsValid) {{ return Results.BadRequest(validation.Errors); }}");
        }
    }

    private static void EmitDeleteHandler(StringBuilder sb, ApiSmithConfig config, NamedTable table, NamedColumn pk, string dbCtx, string repoType, string dbset)
    {
        sb.AppendLine();
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            sb.AppendLine($"        group.MapDelete(\"/{{id}}\", async ({pk.ClrTypeName} id, {dbCtx} db, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var entity = await db.{dbset}.FindAsync(new object[] {{ id }}, ct).ConfigureAwait(false);");
            sb.AppendLine("            if (entity is null) { return Results.NotFound(); }");
            sb.AppendLine($"            db.{dbset}.Remove(entity);");
            sb.AppendLine("            await db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("            return Results.NoContent();");
            sb.AppendLine("        });");
        }
        else
        {
            sb.AppendLine($"        group.MapDelete(\"/{{id}}\", async ({pk.ClrTypeName} id, {repoType} repo, CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var deleted = await repo.DeleteAsync(id, ct).ConfigureAwait(false);");
            sb.AppendLine("            return deleted ? Results.NoContent() : Results.NotFound();");
            sb.AppendLine("        });");
        }
    }
}
