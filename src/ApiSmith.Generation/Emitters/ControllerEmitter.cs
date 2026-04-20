using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class ControllerEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        if (table.PrimaryKey is null)
        {
            return EmitReadOnly(config, layout, table, collidedEntityNames);
        }

        return config.DataAccess is DataAccessStyle.EfCore
            ? EmitEfCore(config, layout, table, collidedEntityNames)
            : EmitDapper(config, layout, table);
    }

    private static EmittedFile EmitEfCore(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var pk = table.PrimaryKey!;
        var dbCtx = $"{config.ProjectName}DbContext";
        var entity = table.EntityName;
        // Schema-prefixed for collided entities; see DbSetNaming.
        var dbset = DbSetNaming.PropertyName(table, collidedEntityNames);
        var route = table.RouteSegment;
        var crud = config.Crud;

        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: true);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{route}\")]");
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {dbCtx} _db;");
        sb.AppendLine();
        sb.AppendLine($"    public {table.CollectionName}Controller({dbCtx} db)");
        sb.AppendLine("    {");
        sb.AppendLine("        _db = db;");
        sb.AppendLine("    }");

        if ((crud & CrudOperations.GetList) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<IReadOnlyList<{entity}Dto>>> List(CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        // Extension point: chain filter/page/sort onto this IQueryable<{entity}>.");
            sb.AppendLine($"        IQueryable<{entity}> query = _db.{dbset}.AsNoTracking();");
            sb.AppendLine("        var items = await query.ToListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        return Ok(items.Select(e => e.ToDto()).ToList());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.GetById) != 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    [HttpGet(\"{{id}}\")]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Dto>> GetById({pk.ClrTypeName} id, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var entity = await _db.{dbset}.AsNoTracking()");
            sb.AppendLine($"            .FirstOrDefaultAsync(e => e.{pk.PropertyName} == id, ct).ConfigureAwait(false);");
            sb.AppendLine("        return entity is null ? NotFound() : Ok(entity.ToDto());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Post) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpPost]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Dto>> Create(Create{entity}Dto dto, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var validator = new Create{entity}DtoValidator();");
            sb.AppendLine("        var validation = validator.Validate(dto);");
            sb.AppendLine("        if (!validation.IsValid) { return BadRequest(validation.Errors); }");
            sb.AppendLine();
            sb.AppendLine("        var entity = dto.ToEntity();");
            sb.AppendLine($"        _db.{dbset}.Add(entity);");
            sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = entity.{pk.PropertyName} }}, entity.ToDto());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Put) != 0)    EmitEfUpdate(sb, "HttpPut", "Update", entity, dbset, pk);
        if ((crud & CrudOperations.Patch) != 0)  EmitEfUpdate(sb, "HttpPatch", "Patch", entity, dbset, pk);

        if ((crud & CrudOperations.Delete) != 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    [HttpDelete(\"{{id}}\")]");
            sb.AppendLine($"    public async Task<IActionResult> Delete({pk.ClrTypeName} id, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var entity = await _db.{dbset}.FindAsync(new object[] {{ id }}, ct).ConfigureAwait(false);");
            sb.AppendLine("        if (entity is null) { return NotFound(); }");
            sb.AppendLine($"        _db.{dbset}.Remove(entity);");
            sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        return NoContent();");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return new EmittedFile(layout.ControllerPath(config, table.CollectionName), sb.ToString());
    }

    private static void EmitEfUpdate(StringBuilder sb, string verb, string actionName, string entity, string dbset, NamedColumn pk)
    {
        sb.AppendLine();
        sb.AppendLine($"    [{verb}(\"{{id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> {actionName}({pk.ClrTypeName} id, Update{entity}Dto dto, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var validator = new Update{entity}DtoValidator();");
        sb.AppendLine("        var validation = validator.Validate(dto);");
        sb.AppendLine("        if (!validation.IsValid) { return BadRequest(validation.Errors); }");
        sb.AppendLine();
        sb.AppendLine($"        var entity = await _db.{dbset}.FindAsync(new object[] {{ id }}, ct).ConfigureAwait(false);");
        sb.AppendLine("        if (entity is null) { return NotFound(); }");
        sb.AppendLine("        entity.UpdateFromDto(dto);");
        sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
    }

    private static EmittedFile EmitDapper(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var pk = table.PrimaryKey!;
        var entity = table.EntityName;
        var route = table.RouteSegment;
        var repoType = $"{entity}Repository";
        var crud = config.Crud;

        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: false);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{route}\")]");
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {repoType} _repo;");
        sb.AppendLine();
        sb.AppendLine($"    public {table.CollectionName}Controller({repoType} repo)");
        sb.AppendLine("    {");
        sb.AppendLine("        _repo = repo;");
        sb.AppendLine("    }");

        if ((crud & CrudOperations.GetList) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<IReadOnlyList<{entity}Dto>>> List(CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var items = await _repo.ListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        return Ok(items.Select(e => e.ToDto()).ToList());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.GetById) != 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    [HttpGet(\"{{id}}\")]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Dto>> GetById({pk.ClrTypeName} id, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
            sb.AppendLine("        return entity is null ? NotFound() : Ok(entity.ToDto());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Post) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpPost]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Dto>> Create(Create{entity}Dto dto, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var validator = new Create{entity}DtoValidator();");
            sb.AppendLine("        var validation = validator.Validate(dto);");
            sb.AppendLine("        if (!validation.IsValid) { return BadRequest(validation.Errors); }");
            sb.AppendLine();
            sb.AppendLine("        var entity = await _repo.CreateAsync(dto.ToEntity(), ct).ConfigureAwait(false);");
            sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = entity.{pk.PropertyName} }}, entity.ToDto());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Put) != 0)    EmitDapperUpdate(sb, "HttpPut", "Update", entity, pk);
        if ((crud & CrudOperations.Patch) != 0)  EmitDapperUpdate(sb, "HttpPatch", "Patch", entity, pk);

        if ((crud & CrudOperations.Delete) != 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    [HttpDelete(\"{{id}}\")]");
            sb.AppendLine($"    public async Task<IActionResult> Delete({pk.ClrTypeName} id, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var deleted = await _repo.DeleteAsync(id, ct).ConfigureAwait(false);");
            sb.AppendLine("        return deleted ? NoContent() : NotFound();");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return new EmittedFile(layout.ControllerPath(config, table.CollectionName), sb.ToString());
    }

    private static void EmitDapperUpdate(StringBuilder sb, string verb, string actionName, string entity, NamedColumn pk)
    {
        sb.AppendLine();
        sb.AppendLine($"    [{verb}(\"{{id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> {actionName}({pk.ClrTypeName} id, Update{entity}Dto dto, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var validator = new Update{entity}DtoValidator();");
        sb.AppendLine("        var validation = validator.Validate(dto);");
        sb.AppendLine("        if (!validation.IsValid) { return BadRequest(validation.Errors); }");
        sb.AppendLine();
        sb.AppendLine("        var entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
        sb.AppendLine("        if (entity is null) { return NotFound(); }");
        sb.AppendLine("        entity.UpdateFromDto(dto);");
        sb.AppendLine("        await _repo.UpdateAsync(entity, ct).ConfigureAwait(false);");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
    }

    private static void EmitCommonUsings(StringBuilder sb, ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, bool efCore)
    {
        var usings = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "Microsoft.AspNetCore.Mvc",
        };
        if (efCore)
        {
            usings.Add("Microsoft.EntityFrameworkCore");
            usings.Add(layout.DataNamespace(config));
        }
        else
        {
            usings.Add(layout.RepositoryNamespace(config));
        }
        usings.Add(layout.DtoNamespace(config, table.Schema));
        usings.Add(layout.EntityNamespace(config, table.Schema));
        usings.Add(layout.MapperNamespace(config, table.Schema));
        usings.Add(layout.ValidatorNamespace(config, table.Schema));

        foreach (var u in usings.OrderBy(u => u, System.StringComparer.Ordinal))
        {
            sb.AppendLine($"using {u};");
        }
    }

    private static EmittedFile EmitReadOnly(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: config.DataAccess is DataAccessStyle.EfCore);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{table.RouteSegment}\")]");
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");

        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            var dbset = DbSetNaming.PropertyName(table, collidedEntityNames);
            sb.AppendLine($"    private readonly {config.ProjectName}DbContext _db;");
            sb.AppendLine();
            sb.AppendLine($"    public {table.CollectionName}Controller({config.ProjectName}DbContext db) {{ _db = db; }}");
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<IReadOnlyList<{table.EntityName}Dto>>> List(CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var items = await _db.{dbset}.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        return Ok(items.Select(e => e.ToDto()).ToList());");
            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine($"    private readonly {table.EntityName}Repository _repo;");
            sb.AppendLine();
            sb.AppendLine($"    public {table.CollectionName}Controller({table.EntityName}Repository repo) {{ _repo = repo; }}");
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<IReadOnlyList<{table.EntityName}Dto>>> List(CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var items = await _repo.ListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        return Ok(items.Select(e => e.ToDto()).ToList());");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return new EmittedFile(layout.ControllerPath(config, table.CollectionName), sb.ToString());
    }
}
