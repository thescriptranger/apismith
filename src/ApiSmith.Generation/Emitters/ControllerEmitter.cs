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
            return config.ApiVersion == ApiVersion.V2
                ? EmitReadOnlyV2(config, layout, table, collidedEntityNames)
                : EmitReadOnlyV1(config, layout, table, collidedEntityNames);
        }

        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            return config.ApiVersion == ApiVersion.V2
                ? EmitEfCoreV2(config, layout, table, collidedEntityNames)
                : EmitEfCoreV1(config, layout, table, collidedEntityNames);
        }

        return config.ApiVersion == ApiVersion.V2
            ? EmitDapperV2(config, layout, table)
            : EmitDapperV1(config, layout, table);
    }

    // ---------- V1: EF Core ----------
    private static EmittedFile EmitEfCoreV1(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var pk = table.PrimaryKey!;
        var dbCtx = $"{config.ProjectName}DbContext";
        var entity = table.EntityName;
        var dbset = DbSetNaming.PropertyName(table, collidedEntityNames);
        var route = table.RouteSegment;
        var crud = config.Crud;
        var injectCreate = (crud & CrudOperations.Post) != 0;
        var injectUpdate = (crud & (CrudOperations.Put | CrudOperations.Patch)) != 0;

        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: true);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{route}\")]");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("[Authorize]");
        }
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {dbCtx} _db;");
        if (injectCreate)
        {
            sb.AppendLine($"    private readonly IValidator<Create{entity}Dto> _createValidator;");
        }
        if (injectUpdate)
        {
            sb.AppendLine($"    private readonly IValidator<Update{entity}Dto> _updateValidator;");
        }
        sb.AppendLine();
        EmitControllerCtorV1(sb, table.CollectionName, $"{dbCtx} db", "        _db = db;", entity, injectCreate, injectUpdate);

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
            sb.AppendLine("        var validation = _createValidator.Validate(dto);");
            AppendBadRequestIfInvalid(sb, config, indent: "        ");
            sb.AppendLine();
            sb.AppendLine("        var entity = dto.ToEntity();");
            sb.AppendLine($"        _db.{dbset}.Add(entity);");
            sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = entity.{pk.PropertyName} }}, entity.ToDto());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Put) != 0)    EmitEfUpdateV1(sb, config, "HttpPut", "Update", entity, dbset, pk);
        if ((crud & CrudOperations.Patch) != 0)  EmitEfUpdateV1(sb, config, "HttpPatch", "Patch", entity, dbset, pk);

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

    private static void EmitEfUpdateV1(StringBuilder sb, ApiSmithConfig config, string verb, string actionName, string entity, string dbset, NamedColumn pk)
    {
        sb.AppendLine();
        sb.AppendLine($"    [{verb}(\"{{id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> {actionName}({pk.ClrTypeName} id, Update{entity}Dto dto, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        var validation = _updateValidator.Validate(dto);");
        AppendBadRequestIfInvalid(sb, config, indent: "        ");
        sb.AppendLine();
        sb.AppendLine($"        var entity = await _db.{dbset}.FindAsync(new object[] {{ id }}, ct).ConfigureAwait(false);");
        sb.AppendLine("        if (entity is null) { return NotFound(); }");
        sb.AppendLine("        entity.UpdateFromDto(dto);");
        sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
    }

    // ---------- V2: EF Core ----------
    private static EmittedFile EmitEfCoreV2(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var pk = table.PrimaryKey!;
        var dbCtx = $"{config.ProjectName}DbContext";
        var entity = table.EntityName;
        var dbset = DbSetNaming.PropertyName(table, collidedEntityNames);
        var route = table.RouteSegment;
        var crud = config.Crud;
        var injectCreate = (crud & CrudOperations.Post) != 0;
        var injectUpdate = (crud & (CrudOperations.Put | CrudOperations.Patch)) != 0;

        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: true);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{route}\")]");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("[Authorize]");
        }
        sb.AppendLine($"public sealed partial class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {dbCtx} _db;");
        if (injectCreate)
        {
            sb.AppendLine($"    private readonly IValidator<Create{entity}Request> _createValidator;");
        }
        if (injectUpdate)
        {
            sb.AppendLine($"    private readonly IValidator<Update{entity}Request> _updateValidator;");
        }
        sb.AppendLine();
        EmitControllerCtorV2(sb, table.CollectionName, $"{dbCtx} db", "        _db = db;", entity, injectCreate, injectUpdate);

        if ((crud & CrudOperations.GetList) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<PagedResponse<{entity}Response>>> List(int page = 1, int pageSize = 50, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (page < 1) { page = 1; }");
            sb.AppendLine("        if (pageSize < 1) { pageSize = 50; }");
            sb.AppendLine($"        // Extension point: chain filter/sort onto this IQueryable<{entity}>.");
            sb.AppendLine($"        IQueryable<{entity}> query = _db.{dbset}.AsNoTracking();");
            sb.AppendLine("        ConfigureListQuery(ref query);");
            sb.AppendLine("        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine($"        return Ok(new PagedResponse<{entity}Response>");
            sb.AppendLine("        {");
            sb.AppendLine("            Items = items.Select(e => e.ToResponse()).ToList(),");
            sb.AppendLine("            Page = page,");
            sb.AppendLine("            PageSize = pageSize,");
            sb.AppendLine("            TotalCount = totalCount,");
            sb.AppendLine("        });");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.GetById) != 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    [HttpGet(\"{{id}}\")]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Response>> GetById({pk.ClrTypeName} id, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var entity = await _db.{dbset}.AsNoTracking()");
            sb.AppendLine($"            .FirstOrDefaultAsync(e => e.{pk.PropertyName} == id, ct).ConfigureAwait(false);");
            sb.AppendLine("        return entity is null ? NotFound() : Ok(entity.ToResponse());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Post) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpPost]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Response>> Create(Create{entity}Request request, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var validation = _createValidator.Validate(request);");
            AppendBadRequestIfInvalid(sb, config, indent: "        ");
            sb.AppendLine();
            sb.AppendLine("        var entity = request.ToEntity();");
            sb.AppendLine($"        _db.{dbset}.Add(entity);");
            sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
            sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = entity.{pk.PropertyName} }}, entity.ToResponse());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Put) != 0)    EmitEfUpdateV2(sb, config, "HttpPut", "Update", entity, dbset, pk);
        if ((crud & CrudOperations.Patch) != 0)  EmitEfUpdateV2(sb, config, "HttpPatch", "Patch", entity, dbset, pk);

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

        sb.AppendLine();
        sb.AppendLine($"    static partial void ConfigureListQuery(ref IQueryable<{entity}> query);");
        sb.AppendLine("}");
        return new EmittedFile(layout.ControllerPath(config, table.CollectionName), sb.ToString());
    }

    private static void EmitEfUpdateV2(StringBuilder sb, ApiSmithConfig config, string verb, string actionName, string entity, string dbset, NamedColumn pk)
    {
        sb.AppendLine();
        sb.AppendLine($"    [{verb}(\"{{id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> {actionName}({pk.ClrTypeName} id, Update{entity}Request request, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        var validation = _updateValidator.Validate(request);");
        AppendBadRequestIfInvalid(sb, config, indent: "        ");
        sb.AppendLine();
        sb.AppendLine($"        var entity = await _db.{dbset}.FindAsync(new object[] {{ id }}, ct).ConfigureAwait(false);");
        sb.AppendLine("        if (entity is null) { return NotFound(); }");
        sb.AppendLine("        entity.UpdateFromRequest(request);");
        sb.AppendLine("        await _db.SaveChangesAsync(ct).ConfigureAwait(false);");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
    }

    // ---------- V1: Dapper ----------
    private static EmittedFile EmitDapperV1(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var pk = table.PrimaryKey!;
        var entity = table.EntityName;
        var route = table.RouteSegment;
        var repoType = config.EmitRepositoryInterfaces ? $"I{entity}Repository" : $"{entity}Repository";
        var crud = config.Crud;
        var injectCreate = (crud & CrudOperations.Post) != 0;
        var injectUpdate = (crud & (CrudOperations.Put | CrudOperations.Patch)) != 0;

        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: false);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{route}\")]");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("[Authorize]");
        }
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {repoType} _repo;");
        if (injectCreate)
        {
            sb.AppendLine($"    private readonly IValidator<Create{entity}Dto> _createValidator;");
        }
        if (injectUpdate)
        {
            sb.AppendLine($"    private readonly IValidator<Update{entity}Dto> _updateValidator;");
        }
        sb.AppendLine();
        EmitControllerCtorV1(sb, table.CollectionName, $"{repoType} repo", "        _repo = repo;", entity, injectCreate, injectUpdate);

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
            sb.AppendLine("        var validation = _createValidator.Validate(dto);");
            AppendBadRequestIfInvalid(sb, config, indent: "        ");
            sb.AppendLine();
            sb.AppendLine("        var entity = await _repo.CreateAsync(dto.ToEntity(), ct).ConfigureAwait(false);");
            sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = entity.{pk.PropertyName} }}, entity.ToDto());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Put) != 0)    EmitDapperUpdateV1(sb, config, "HttpPut", "Update", entity, pk);
        if ((crud & CrudOperations.Patch) != 0)  EmitDapperUpdateV1(sb, config, "HttpPatch", "Patch", entity, pk);

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

    private static void EmitDapperUpdateV1(StringBuilder sb, ApiSmithConfig config, string verb, string actionName, string entity, NamedColumn pk)
    {
        sb.AppendLine();
        sb.AppendLine($"    [{verb}(\"{{id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> {actionName}({pk.ClrTypeName} id, Update{entity}Dto dto, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        var validation = _updateValidator.Validate(dto);");
        AppendBadRequestIfInvalid(sb, config, indent: "        ");
        sb.AppendLine();
        sb.AppendLine("        var entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
        sb.AppendLine("        if (entity is null) { return NotFound(); }");
        sb.AppendLine("        entity.UpdateFromDto(dto);");
        sb.AppendLine("        await _repo.UpdateAsync(entity, ct).ConfigureAwait(false);");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
    }

    // ---------- V2: Dapper ----------
    private static EmittedFile EmitDapperV2(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var pk = table.PrimaryKey!;
        var entity = table.EntityName;
        var route = table.RouteSegment;
        var repoType = config.EmitRepositoryInterfaces ? $"I{entity}Repository" : $"{entity}Repository";
        var crud = config.Crud;
        var injectCreate = (crud & CrudOperations.Post) != 0;
        var injectUpdate = (crud & (CrudOperations.Put | CrudOperations.Patch)) != 0;

        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: false);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{route}\")]");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("[Authorize]");
        }
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {repoType} _repo;");
        if (injectCreate)
        {
            sb.AppendLine($"    private readonly IValidator<Create{entity}Request> _createValidator;");
        }
        if (injectUpdate)
        {
            sb.AppendLine($"    private readonly IValidator<Update{entity}Request> _updateValidator;");
        }
        sb.AppendLine();
        EmitControllerCtorV2(sb, table.CollectionName, $"{repoType} repo", "        _repo = repo;", entity, injectCreate, injectUpdate);

        if ((crud & CrudOperations.GetList) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<PagedResponse<{entity}Response>>> List(int page = 1, int pageSize = 50, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (page < 1) { page = 1; }");
            sb.AppendLine("        if (pageSize < 1) { pageSize = 50; }");
            sb.AppendLine("        // Known limitation: Dapper IXRepository.ListAsync returns all rows; slicing happens client-side.");
            sb.AppendLine("        // If IXRepository gains paging, replace the slice with a server-side fetch.");
            sb.AppendLine("        var all = await _repo.ListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        var totalCount = all.Count;");
            sb.AppendLine("        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();");
            sb.AppendLine($"        return Ok(new PagedResponse<{entity}Response>");
            sb.AppendLine("        {");
            sb.AppendLine("            Items = items.Select(e => e.ToResponse()).ToList(),");
            sb.AppendLine("            Page = page,");
            sb.AppendLine("            PageSize = pageSize,");
            sb.AppendLine("            TotalCount = totalCount,");
            sb.AppendLine("        });");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.GetById) != 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    [HttpGet(\"{{id}}\")]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Response>> GetById({pk.ClrTypeName} id, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
            sb.AppendLine("        return entity is null ? NotFound() : Ok(entity.ToResponse());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Post) != 0)
        {
            sb.AppendLine();
            sb.AppendLine("    [HttpPost]");
            sb.AppendLine($"    public async Task<ActionResult<{entity}Response>> Create(Create{entity}Request request, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var validation = _createValidator.Validate(request);");
            AppendBadRequestIfInvalid(sb, config, indent: "        ");
            sb.AppendLine();
            sb.AppendLine("        var entity = await _repo.CreateAsync(request.ToEntity(), ct).ConfigureAwait(false);");
            sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = entity.{pk.PropertyName} }}, entity.ToResponse());");
            sb.AppendLine("    }");
        }

        if ((crud & CrudOperations.Put) != 0)    EmitDapperUpdateV2(sb, config, "HttpPut", "Update", entity, pk);
        if ((crud & CrudOperations.Patch) != 0)  EmitDapperUpdateV2(sb, config, "HttpPatch", "Patch", entity, pk);

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

    private static void EmitDapperUpdateV2(StringBuilder sb, ApiSmithConfig config, string verb, string actionName, string entity, NamedColumn pk)
    {
        sb.AppendLine();
        sb.AppendLine($"    [{verb}(\"{{id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> {actionName}({pk.ClrTypeName} id, Update{entity}Request request, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        var validation = _updateValidator.Validate(request);");
        AppendBadRequestIfInvalid(sb, config, indent: "        ");
        sb.AppendLine();
        sb.AppendLine("        var entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);");
        sb.AppendLine("        if (entity is null) { return NotFound(); }");
        sb.AppendLine("        entity.UpdateFromRequest(request);");
        sb.AppendLine("        await _repo.UpdateAsync(entity, ct).ConfigureAwait(false);");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
    }

    // ---------- Constructors ----------
    private static void EmitControllerCtorV1(
        StringBuilder sb,
        string collectionName,
        string primaryParam,
        string primaryAssignment,
        string entity,
        bool injectCreate,
        bool injectUpdate)
    {
        var parameters = new List<string> { primaryParam };
        if (injectCreate)
        {
            parameters.Add($"IValidator<Create{entity}Dto> createValidator");
        }
        if (injectUpdate)
        {
            parameters.Add($"IValidator<Update{entity}Dto> updateValidator");
        }

        sb.AppendLine($"    public {collectionName}Controller({string.Join(", ", parameters)})");
        sb.AppendLine("    {");
        sb.AppendLine(primaryAssignment);
        if (injectCreate)
        {
            sb.AppendLine("        _createValidator = createValidator;");
        }
        if (injectUpdate)
        {
            sb.AppendLine("        _updateValidator = updateValidator;");
        }
        sb.AppendLine("    }");
    }

    private static void EmitControllerCtorV2(
        StringBuilder sb,
        string collectionName,
        string primaryParam,
        string primaryAssignment,
        string entity,
        bool injectCreate,
        bool injectUpdate)
    {
        var parameters = new List<string> { primaryParam };
        if (injectCreate)
        {
            parameters.Add($"IValidator<Create{entity}Request> createValidator");
        }
        if (injectUpdate)
        {
            parameters.Add($"IValidator<Update{entity}Request> updateValidator");
        }

        sb.AppendLine($"    public {collectionName}Controller({string.Join(", ", parameters)})");
        sb.AppendLine("    {");
        sb.AppendLine(primaryAssignment);
        if (injectCreate)
        {
            sb.AppendLine("        _createValidator = createValidator;");
        }
        if (injectUpdate)
        {
            sb.AppendLine("        _updateValidator = updateValidator;");
        }
        sb.AppendLine("    }");
    }

    private static void AppendBadRequestIfInvalid(StringBuilder sb, ApiSmithConfig config, string indent)
    {
        if (config.ApiVersion == ApiVersion.V2)
        {
            sb.AppendLine($"{indent}if (!validation.IsValid) {{ return BadRequest(new ApiProblem(\"Validation failed\", 400, \"https://apismith.dev/problems/validation\", validation.Errors.ToImmutableArray())); }}");
        }
        else
        {
            sb.AppendLine($"{indent}if (!validation.IsValid) {{ return BadRequest(validation.Errors); }}");
        }
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
        // IValidator<TDto> lives in the unsegmented validator core namespace; match ValidationResult.cs.
        usings.Add(layout.ValidatorCoreNamespace(config));
        if (config.ApiVersion == ApiVersion.V2)
        {
            usings.Add("System.Collections.Immutable");
            usings.Add(layout.SharedErrorsNamespace(config));
            // Request types live under `<Shared>.Requests[.<Schema>]`; non-view tables reference them in Create/Update.
            if (table.PrimaryKey is not null)
            {
                usings.Add(layout.RequestNamespace(config, table.Schema));
            }
            // Response types live under `<Shared>.Responses[.<Schema>]`.
            usings.Add(layout.ResponseNamespace(config, table.Schema));
            // PagedResponse<T> lives at the root of `<Shared>.Responses`.
            usings.Add($"{layout.SharedNamespace(config)}.Responses");
        }
        if (config.Auth != AuthStyle.None)
        {
            usings.Add("Microsoft.AspNetCore.Authorization");
        }

        foreach (var u in usings.OrderBy(u => u, System.StringComparer.Ordinal))
        {
            sb.AppendLine($"using {u};");
        }
    }

    // ---------- V1: Read-only (views) ----------
    private static EmittedFile EmitReadOnlyV1(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: config.DataAccess is DataAccessStyle.EfCore);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{table.RouteSegment}\")]");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("[Authorize]");
        }
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
            var roRepoType = config.EmitRepositoryInterfaces
                ? $"I{table.EntityName}Repository"
                : $"{table.EntityName}Repository";
            sb.AppendLine($"    private readonly {roRepoType} _repo;");
            sb.AppendLine();
            sb.AppendLine($"    public {table.CollectionName}Controller({roRepoType} repo) {{ _repo = repo; }}");
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

    // ---------- V2: Read-only (views) ----------
    private static EmittedFile EmitReadOnlyV2(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table, IReadOnlySet<string> collidedEntityNames)
    {
        var sb = new StringBuilder();
        EmitCommonUsings(sb, config, layout, table, efCore: config.DataAccess is DataAccessStyle.EfCore);

        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ControllerNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{VersioningEmitter.RoutePrefix(config)}/{table.RouteSegment}\")]");
        if (config.Auth != AuthStyle.None)
        {
            sb.AppendLine("[Authorize]");
        }
        sb.AppendLine($"public sealed class {table.CollectionName}Controller : ControllerBase");
        sb.AppendLine("{");

        var entity = table.EntityName;

        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            var dbset = DbSetNaming.PropertyName(table, collidedEntityNames);
            sb.AppendLine($"    private readonly {config.ProjectName}DbContext _db;");
            sb.AppendLine();
            sb.AppendLine($"    public {table.CollectionName}Controller({config.ProjectName}DbContext db) {{ _db = db; }}");
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<PagedResponse<{entity}Response>>> List(int page = 1, int pageSize = 50, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (page < 1) { page = 1; }");
            sb.AppendLine("        if (pageSize < 1) { pageSize = 50; }");
            sb.AppendLine($"        IQueryable<{entity}> query = _db.{dbset}.AsNoTracking();");
            sb.AppendLine("        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine($"        return Ok(new PagedResponse<{entity}Response>");
            sb.AppendLine("        {");
            sb.AppendLine("            Items = items.Select(e => e.ToResponse()).ToList(),");
            sb.AppendLine("            Page = page,");
            sb.AppendLine("            PageSize = pageSize,");
            sb.AppendLine("            TotalCount = totalCount,");
            sb.AppendLine("        });");
            sb.AppendLine("    }");
        }
        else
        {
            var roRepoType = config.EmitRepositoryInterfaces
                ? $"I{entity}Repository"
                : $"{entity}Repository";
            sb.AppendLine($"    private readonly {roRepoType} _repo;");
            sb.AppendLine();
            sb.AppendLine($"    public {table.CollectionName}Controller({roRepoType} repo) {{ _repo = repo; }}");
            sb.AppendLine();
            sb.AppendLine("    [HttpGet]");
            sb.AppendLine($"    public async Task<ActionResult<PagedResponse<{entity}Response>>> List(int page = 1, int pageSize = 50, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (page < 1) { page = 1; }");
            sb.AppendLine("        if (pageSize < 1) { pageSize = 50; }");
            sb.AppendLine("        // Known limitation: Dapper IXRepository.ListAsync returns all rows; slicing happens client-side.");
            sb.AppendLine("        var all = await _repo.ListAsync(ct).ConfigureAwait(false);");
            sb.AppendLine("        var totalCount = all.Count;");
            sb.AppendLine("        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();");
            sb.AppendLine($"        return Ok(new PagedResponse<{entity}Response>");
            sb.AppendLine("        {");
            sb.AppendLine("            Items = items.Select(e => e.ToResponse()).ToList(),");
            sb.AppendLine("            Page = page,");
            sb.AppendLine("            PageSize = pageSize,");
            sb.AppendLine("            TotalCount = totalCount,");
            sb.AppendLine("        });");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return new EmittedFile(layout.ControllerPath(config, table.CollectionName), sb.ToString());
    }
}
