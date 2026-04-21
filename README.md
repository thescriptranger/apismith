# ApiSmith

A `dotnet` global tool that reads an existing SQL Server database and scaffolds a working .NET API from it. Entities, DTOs, validators, mappers, endpoints, DbContext, tests, Docker, the lot. Runs on first execution with no manual edits.

Only Microsoft packages in the generated code. Scalar for the OpenAPI UI. Dapper if you ask for it. No AutoMapper, no MediatR, no source generators.

> **Status**: not yet published. Current builds are local. The tool works end-to-end; NuGet publish is pending a license decision.

## Install

```bash
dotnet tool install -g ApiSmith
```

Needs the .NET 8 SDK or later on the host. Output can target whatever `dotnet --list-sdks` shows. Windows, Linux, macOS.

## Quick start

Point it at a database:

```sql
CREATE TABLE customers (
    id int IDENTITY PRIMARY KEY,
    email nvarchar(256) NOT NULL,
    display_name nvarchar(100) NULL,
    CONSTRAINT UX_customers_email UNIQUE (email)
);

CREATE TABLE orders (
    id int IDENTITY PRIMARY KEY,
    customer_id int NOT NULL,
    status nvarchar(20) NOT NULL,
    total_cents int NOT NULL,
    CONSTRAINT FK_orders_customer FOREIGN KEY (customer_id) REFERENCES customers(id),
    CONSTRAINT CK_orders_total_nonneg CHECK (total_cents >= 0)
);
```

Run:

```bash
apismith new
```

Answer thirteen prompts. A solution lands on disk. Then:

```bash
cd CustomerDemoApi
dotnet run --project src/CustomerDemoApi
```

`http://localhost:5000/scalar/v1` has the OpenAPI UI. Check the generated `apismith.yaml` into git; `apismith new --config apismith.yaml --connection "..."` regenerates byte-identical output on any machine.

## The wizard

Thirteen prompts, in order:

| # | Prompt | Default |
|---|---|---|
| 1 | Project name | `MyApi` |
| 2 | Output directory | `./<ProjectName>` |
| 3 | Target framework | newest GA SDK detected on the host |
| 4 | Endpoint style | `Controllers` or `MinimalApi` |
| 5 | Architecture | `Flat`, `Clean`, `VerticalSlice`, `Layered`, or `Onion` |
| 6 | Data access | `EfCore` or `Dapper` |
| 7 | Generate initial migration | bootstrap scripts for `dotnet ef migrations add InitialCreate` |
| 8 | CRUD operations | multi-select: GET list / GET by id / POST / PUT / PATCH / DELETE |
| 9 | API versioning | `None`, `UrlSegment`, `Header`, `QueryString` |
| 10 | Auth | `None`, `JwtBearer`, `Auth0`, `AzureAd`, `ApiKey` |
| 11 | Include tests project | xUnit + `WebApplicationFactory` + EF Core InMemory |
| 12 | Include Docker assets | Dockerfile + compose with SQL Server 2022 |
| 13 | Schemas to include | multi-select from discovered schemas |

Every wizard run writes `apismith.yaml` to the output directory. Check it in. Connection strings never go in the file.

## Connection string resolution

In order:

1. `--connection` flag
2. `APISMITH_CONNECTION` env var
3. `connectionString:` in `apismith.yaml` (only if you edited it in manually)

## What gets generated

Flat architecture, Controllers, EF Core, tests on, Docker on, for a small blog schema:

```
BlogApi/
├── apismith.yaml
├── BlogApi.sln
├── README.md
├── .gitignore .editorconfig
├── Dockerfile docker-compose.yml
├── scripts/
│   ├── add-initial-migration.ps1
│   └── add-initial-migration.sh
├── src/BlogApi/
│   ├── BlogApi.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Entities/{User,Post,Tag}.cs
│   ├── Dtos/{User,Post,Tag}Dtos.cs
│   ├── Validators/{ValidationResult,User,Post,Tag}*.cs
│   ├── Mappings/{User,Post,Tag}Mappings.cs
│   ├── Controllers/{Users,Posts,Tags}Controller.cs
│   └── Data/BlogApiDbContext.cs
└── tests/BlogApi.IntegrationTests/
    ├── TestWebApplicationFactory.cs
    ├── Validators/{User,Post,Tag}ValidatorTests.cs
    └── Endpoints/{Users,Posts,Tags}EndpointTests.cs
```

Every emitted project sets `TreatWarningsAsErrors=true` and `Nullable=enable`. If the generated code trips a warning, that's a bug.

## Shared contracts project

New scaffolds default to `apiVersion: v2`, which emits a `<Name>.Shared` class-library project holding the API's wire contracts:

- **`Requests/`** — `Create<Entity>Request` and `Update<Entity>Request` with DataAnnotations (`[Required]`, `[StringLength]`, `[Range]`).
- **`Responses/`** — `<Entity>Response` (no attributes) plus a generic `PagedResponse<T>` envelope.
- **`Enums/`** — C# enums derived from `CHECK IN` constraints.
- **`Errors/`** — `ValidationError` and `ApiProblem` for typed 400 responses.

BCL-only — no `Microsoft.AspNetCore.*` dependency — so it packs cleanly as a NuGet for pure-console clients.

Server-side Dtos (the Application-layer working model) live at their conventional location (e.g. `src/<Name>/Dtos/` for Flat, `src/<Name>.Application/Dtos/` for Clean) and are distinct from the Request/Response wire contracts. Mappers hand-roll the two hops Entity ↔ Dto and Dto ↔ Response, with partial `OnMapped` hooks users can extend in sibling files.

List endpoints paginate by default with `page=1`, `pageSize=50`. Pass `?pageSize=1000` (or whatever) for larger pages.

Existing configs stay on `apiVersion: v1` (no Shared project, DTOs in the server assembly, flat lists). To adopt v2 on an existing scaffold, edit the `apismith.yaml` line to `apiVersion: v2` and rerun.

## Architectures

Same inputs, different folder structure. Pick the one your team uses.

- **Flat** — single project, folders by concern. Fine for small services.
- **Clean** — `Api` / `Application` / `Domain` / `Infrastructure`. Entities in Domain, validators/mappers/handlers in Application.
- **VerticalSlice** — `Features/<Entity>/<Action>/` with Request, Handler, Validator, Endpoint, Mapping colocated. Includes a hand-rolled dispatcher (no MediatR).
- **Layered** — `Api` / `BusinessLogic` / `DataAccess`. N-tier.
- **Onion** — `Api` / `Services` / `Domain` / `Infrastructure`. Same rough shape as Clean with different names.

Every combination of architecture × endpoint style × data access × auth × versioning passes a matrix test that emits the solution and runs `dotnet build` on it.

## Endpoint styles

- **Controllers** — `[ApiController]` + attribute routing, six actions per entity.
- **MinimalApi** — `app.Map<Collection>Endpoints(...)` extension methods, one static class per entity.

Identical HTTP surface; the same smoke tests pass against either.

## Data access

- **EfCore** — `Microsoft.EntityFrameworkCore.SqlServer`. DbContext with full `OnModelCreating` (table names, schemas, PKs, FKs, navigation props, unique constraints, indexes, check constraints, sequences, skip navs for join tables).
- **Dapper** — `<Entity>Repository` per entity with hand-written SQL. `SqlConnectionFactory` in DI. Dapper is the only non-Microsoft runtime package allowed, and only when you opt in.

## Validation

One validator per Create/Update type. Imperative, debuggable, no attributes. Under v2 the validators target Request types (`CreatePostRequestValidator` etc.); under v1 they continue to validate Dtos.

```csharp
public sealed class CreatePostDtoValidator
{
    public ValidationResult Validate(CreatePostDto dto)
    {
        var result = new ValidationResult();
        if (dto is null) { result.Add("", "DTO must not be null."); return result; }

        if (string.IsNullOrWhiteSpace(dto.Title))
            result.Add(nameof(dto.Title), "Title is required.");

        if (dto.Title is not null && dto.Title.Length > 200)
            result.Add(nameof(dto.Title), "Title must be 200 characters or fewer.");

        // From SQL check constraint 'CK_orders_total_nonneg': ([total_cents] >= 0)
        if (dto.TotalCents < 0)
            result.Add(nameof(dto.TotalCents), "TotalCents must be >= 0.");

        return result;
    }
}
```

Rules are derived from the schema:

- `NOT NULL` string → required check
- `nvarchar(N)` → max-length check
- `CHECK (col >= N)`, `CHECK (col BETWEEN a AND b)` → translated to typed range checks
- Untranslatable CHECKs (IN lists, ORs, function calls) → emit a `// TODO` comment with the raw SQL
- Optional `ValidateForeignKeyReferences` flag → emits a `default`-value check on required FK columns with a `// TODO` to wire up the real existence query

Endpoints call the validator first and return `400 Bad Request` with the error list on failure. No exceptions on validation failure — it's data.

## Mapping

Hand-written extension methods per entity. No AutoMapper. No Mapster. No reflection.

Under v1, mappers hop between Entity and Dto:

```csharp
public static class PostMappings
{
    public static PostDto ToDto(this Post entity) => new() { Id = entity.Id, /* ... */ };
    public static Post ToEntity(this CreatePostDto dto) => new() { /* ... */ };
    public static void UpdateFromDto(this Post entity, UpdatePostDto dto) { /* ... */ }
}
```

Under v2, mappers hand-roll two hops — Entity ↔ Dto (server working model) and Dto ↔ Response/Request (wire contracts) — with partial `OnMapped` hooks users can extend in sibling files.

## Auth

- **None** — anonymous.
- **JwtBearer** — `Microsoft.AspNetCore.Authentication.JwtBearer` + `[Authorize]` on endpoints.
- **Auth0** / **AzureAd** — same JwtBearer setup with issuer/audience pre-wired.
- **ApiKey** — hand-rolled middleware validating `X-API-Key`.

All Microsoft packages.

## API versioning

- **None** — `/api/<collection>`.
- **UrlSegment** — `/api/v1/<collection>`.
- **Header** — `X-Api-Version: 1` via middleware.
- **QueryString** — `?api-version=1` via middleware.

Hand-rolled, no third-party versioning packages.

## CRUD selection

Prompt #8 is a multi-select. Drop the operations you don't want. Read-only tables can ship with just GET list + GET by id; the validator branches for the unused operations aren't emitted.

`GET /resource` returns everything by default. The `IQueryable` (EF) or SQL query (Dapper) is exposed inside a marked extension point:

```csharp
[HttpGet]
public async Task<ActionResult<IReadOnlyList<PostDto>>> List(CancellationToken ct)
{
    // Extension point: chain .Where/.OrderBy/.Skip/.Take here before ToListAsync.
    var items = await _db.Posts.AsNoTracking().ToListAsync(ct);
    return Ok(items.Select(e => e.ToDto()).ToList());
}
```

No generated pagination, filtering, or sorting. Those are product decisions. The comment tells you where to add them.

## Stored procedures, views, functions

**Stored procedures** → `IStoredProcedures` service with typed param and result classes. Result shape inferred from `sys.sp_describe_first_result_set`. Dynamic SQL / temp tables / conditional branches defeat inference; you get a stub result class with a TODO and a warning in the scaffold log. Dapper path is fully implemented; EF Core path returns `Array.Empty<T>()` with a TODO because EF's sproc story is case-by-case.

**Views** → read-only entities, `b.ToView().HasNoKey()`, GET-only endpoints.

**Functions** → `IDbFunctions` service with typed signatures. Bodies throw `NotImplementedException` — the signature's there, the implementation is yours.

## Multi-schema

Single-schema `dbo` DBs stay flat: `namespace MyApi.Entities`, files at `src/MyApi/Entities/`.

Multi-schema DBs get a schema segment per non-default schema:

```
src/MyApi/Entities/Audit/UserAction.cs   →  namespace MyApi.Entities.Audit
src/MyApi/Entities/Post.cs               →  namespace MyApi.Entities        (dbo stays flat)
```

`dbo` stays unsegmented even in a multi-schema DB — it's the default, and nesting it adds noise. Exact predicate: segment emitted when `Schemas.Count > 1` OR `schema != "dbo"`.

**Cross-schema navigations** get the right `using`:

```csharp
using MyApi.Entities;   // for dbo.User

namespace MyApi.Entities.Audit;

public sealed class UserAction
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
```

**Name collisions across schemas** (e.g. `dbo.User` + `audit.User`) get schema-prefixed DbSet property names and fully-qualified types in the DbContext:

```csharp
public DbSet<MyApi.Entities.User> DboUsers => Set<MyApi.Entities.User>();
public DbSet<MyApi.Entities.Audit.User> AuditUsers => Set<MyApi.Entities.Audit.User>();
```

Controllers, Minimal API endpoints, and test smokes route through a single `DbSetNaming` helper so the disambiguation stays consistent.

## Migrations

Opt in at prompt #7. Get `scripts/add-initial-migration.ps1` (Windows) and `scripts/add-initial-migration.sh` (Linux/macOS). Either script installs `dotnet-ef` if missing, runs `dotnet ef migrations add InitialCreate`, and generates the idempotent SQL that bootstraps `__EFMigrationsHistory` so the live DB is treated as already-migrated. Both scripts run the same `dotnet ef` commands.

## Tests project

Opt in at prompt #11. Packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`.

**Validator tests** per entity:

- `Rejects_null_dto`
- `Accepts_minimally_valid_dto` — seeds required strings, required FKs set to `1`, check-constraint columns set to a valid value
- `Rejects_default_foreign_key` — emitted if `ValidateForeignKeyReferences` is on and the entity has a required FK
- `Rejects_value_outside_check_constraint_<Col>` — one per translated check constraint

**Endpoint smokes** per entity (EF Core path only; Dapper needs a real DB):

- `Get_list_returns_ok`
- `Get_by_id_returns_404_when_missing`
- `Post_returns_success_with_valid_payload`
- `Put_returns_404_when_id_missing`
- `Patch_returns_404_when_id_missing`
- `Delete_returns_404_when_id_missing`

All run against an in-memory DB swapped in by `TestWebApplicationFactory`.

## Docker

Opt in at prompt #12.

`Dockerfile` is multi-stage with `mcr.microsoft.com/dotnet/sdk` → `mcr.microsoft.com/dotnet/aspnet`, matching the TFM you picked.

`docker-compose.yml` brings up the API + SQL Server 2022, wired by env vars, with a DB healthcheck and a persistent volume. `docker compose up` and the API's reachable on a local port with the schema provisioned.

## OpenAPI

`Microsoft.AspNetCore.OpenApi` (built-in on .NET 9+) for the spec. Scalar (`Scalar.AspNetCore`) for the UI at `/scalar/v1`. Scalar is the only third-party web-project dependency allowed; Microsoft doesn't publish an equivalent.

## CLI reference

```bash
apismith new                                        # wizard
apismith new --config apismith.yaml --connection "..."
apismith new --name MyApi --connection "..."         # scripted, no config file
apismith --version
apismith --help
```

| Flag | Short | Notes |
|---|---|---|
| `--config` | `-c` | Path to `apismith.yaml`. Triggers replay. |
| `--connection` | | Precedence: flag > env > yaml. |
| `--name` | `-n` | Scripted mode only. |
| `--output` | `-o` | Overrides the config's output dir. |
| `--schema` | `-s` | Include a schema. Repeatable. Scripted mode only. |

`APISMITH_CONNECTION` env var is the fallback when `--connection` is omitted.

## Determinism

Two runs with the same `apismith.yaml`, same schema, and same ApiSmith version produce byte-identical output. Every reader sorts by `StringComparer.Ordinal`, every emitter iterates already-sorted collections, templates don't embed timestamps or GUIDs.

Useful for:

- Checking `apismith.yaml` into git and letting teammates regenerate.
- Diffing regenerated output against the committed scaffold to spot schema drift.
- Running ApiSmith in CI on a schedule.

The `Replay_is_byte_identical` test runs the generator twice and compares output byte-for-byte. It's load-bearing.

## Supported

| | |
|---|---|
| Database | SQL Server 2017+ |
| .NET SDK on host | 8.0+ |
| Generated TFMs | 8.0, 9.0, any SDK on the host |
| OS | Windows 10+, Linux (.NET 8+), macOS 11+ |
| CPU | x64, ARM64 |

PostgreSQL, MySQL, SQLite — roadmap.

## Roadmap

- PostgreSQL.
- Re-generation with merge/diff. (Currently scaffold-once.)
- gRPC and GraphQL endpoints.
- Auth0 Terraform config alongside the .NET code.
- Helm/K8s manifests.

Not planned:

- Business logic. ApiSmith does plumbing.
- Background jobs, caches, observability beyond default logging.
- Multi-tenancy.
- Frontend.

## Build

```bash
git clone https://github.com/<org>/apismith.git
cd apismith
dotnet build
dotnet test
```

Tests split three ways:

- `tests/ApiSmith.UnitTests/` — pure logic (naming, YAML, translator, probes).
- `tests/ApiSmith.Introspection.Tests/` — reader row-shaping.
- `tests/ApiSmith.Generation.Tests/` — emit a scaffold from an in-memory schema model and run `dotnet build` on it. Slow (~30s) because of the nested build. Set `APISMITH_SKIP_NESTED_BUILD=1` for the fast loop; re-enable before pushing.

Current: **214 tests, all green.**

### Repo layout

```
src/
├── ApiSmith.Cli/              # entry point, packs as global tool
├── ApiSmith.Core/             # schema model + pipeline abstractions
├── ApiSmith.Console/          # hand-rolled prompts
├── ApiSmith.Introspection/    # per-concept SQL readers
├── ApiSmith.Naming/           # casing, pluralization, schema segments
├── ApiSmith.Templating/       # .apismith mini-DSL
├── ApiSmith.Generation/       # generator + emitters + 5 layouts
├── ApiSmith.Templates/        # embedded .apismith files
└── ApiSmith.Config/           # apismith.yaml reader/writer

tests/
├── ApiSmith.UnitTests/
├── ApiSmith.Introspection.Tests/
├── ApiSmith.Generation.Tests/
└── ApiSmith.EndToEnd.Tests/   # reserved for live-DB runs
```

### Contributing

Issue first for anything bigger than a typo. Every PR:

1. Tests for the behavior you changed.
2. Zero warnings, zero errors under strict mode.
3. Full `dotnet test` green.
4. `Replay_is_byte_identical` stays green. Don't break that one.

## License

TBD (MIT or Apache 2.0 — haven't decided).
