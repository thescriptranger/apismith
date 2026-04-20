using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Naming;

namespace ApiSmith.Generation.Emitters;

public static class ProgramCsEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named, SchemaGraph graph)
    {
        var usings = new SortedSet<string>(System.StringComparer.Ordinal)
        {
            "Scalar.AspNetCore",
        };
        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            usings.Add("Microsoft.EntityFrameworkCore");
            usings.Add(layout.DataNamespace(config));
        }
        else
        {
            usings.Add(layout.DataNamespace(config));
            usings.Add(layout.RepositoryNamespace(config));
        }
        if (config.EndpointStyle is EndpointStyle.MinimalApi)
        {
            usings.Add(layout.EndpointNamespace(config));
        }
        if (config.Architecture is ArchitectureStyle.VerticalSlice)
        {
            usings.Add(layout.DispatcherNamespace(config));
        }
        foreach (var u in AuthEmitter.ExtraUsings(config, layout))
        {
            usings.Add(u);
        }
        if (config.Versioning is VersioningStyle.Header or VersioningStyle.QueryString)
        {
            usings.Add($"{layout.ApiNamespace(config)}.Versioning");
        }

        // Validator DI registrations. Collect once so we can emit the matching usings up top
        // and the AddScoped lines below. Entities whose name collides across schemas get fully
        // qualified at the call site; all others rely on a per-namespace using directive.
        var validatedTables = ValidatedTables(config, named).ToList();
        var collidedValidatorEntities = validatedTables
            .GroupBy(t => t.EntityName, System.StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(System.StringComparer.Ordinal);
        if (validatedTables.Count > 0)
        {
            usings.Add(layout.ValidatorCoreNamespace(config));
            foreach (var t in validatedTables)
            {
                if (collidedValidatorEntities.Contains(t.EntityName))
                {
                    continue; // fully qualified at the call site — no using needed
                }
                usings.Add(layout.DtoNamespace(config, t.Schema));
                usings.Add(layout.ValidatorNamespace(config, t.Schema));
            }
        }

        var sb = new StringBuilder();
        foreach (var u in usings)
        {
            sb.AppendLine($"using {u};");
        }
        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        if (config.EndpointStyle is EndpointStyle.Controllers)
        {
            sb.AppendLine("builder.Services.AddControllers();");
        }
        sb.AppendLine("builder.Services.AddOpenApi();");
        sb.AppendLine();

        var authServices = AuthEmitter.ServiceRegistrations(config).ToList();
        foreach (var line in authServices)
        {
            sb.AppendLine(line);
        }
        if (authServices.Count > 0)
        {
            sb.AppendLine();
        }

        var dbCtx = $"{config.ProjectName}DbContext";

        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            sb.AppendLine($"builder.Services.AddDbContext<{dbCtx}>(options =>");
            sb.AppendLine("{");
            sb.AppendLine("    var connectionString = builder.Configuration.GetConnectionString(\"DefaultConnection\")");
            sb.AppendLine("        ?? throw new InvalidOperationException(\"Missing connection string 'DefaultConnection' in configuration.\");");
            sb.AppendLine("    options.UseSqlServer(connectionString);");
            sb.AppendLine("});");
        }
        else
        {
            sb.AppendLine("builder.Services.AddSingleton<IDbConnectionFactory, SqlDbConnectionFactory>();");
            foreach (var t in named.Tables)
            {
                if (config.EmitRepositoryInterfaces)
                {
                    sb.AppendLine($"builder.Services.AddScoped<I{t.EntityName}Repository, {t.EntityName}Repository>();");
                }
                else
                {
                    sb.AppendLine($"builder.Services.AddScoped<{t.EntityName}Repository>();");
                }
            }
        }

        if (config.Architecture is ArchitectureStyle.VerticalSlice)
        {
            sb.AppendLine("builder.Services.AddDispatcher(typeof(Program).Assembly);");
            sb.AppendLine("builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));");
        }

        if (config.PartitionStoredProceduresBySchema)
        {
            var sprocSchemas = graph.Schemas
                .Where(s => s.Procedures.Length > 0)
                .Select(s => Casing.ToPascal(s.Name))
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToList();
            foreach (var schemaPascal in sprocSchemas)
            {
                sb.AppendLine($"builder.Services.AddScoped<I{schemaPascal}StoredProcedures, {schemaPascal}StoredProcedures>();");
            }

            var fnSchemas = graph.Schemas
                .Where(s => s.Functions.Length > 0)
                .Select(s => Casing.ToPascal(s.Name))
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToList();
            foreach (var schemaPascal in fnSchemas)
            {
                sb.AppendLine($"builder.Services.AddScoped<I{schemaPascal}DbFunctions, {schemaPascal}DbFunctions>();");
            }
        }

        if (validatedTables.Count > 0)
        {
            sb.AppendLine();
            var crud = config.Crud;
            var registerCreate = (crud & CrudOperations.Post) != 0;
            var registerUpdate = (crud & (CrudOperations.Put | CrudOperations.Patch)) != 0;
            foreach (var t in validatedTables)
            {
                var qualify = collidedValidatorEntities.Contains(t.EntityName);
                var dtoPrefix = qualify ? layout.DtoNamespace(config, t.Schema) + "." : string.Empty;
                var valPrefix = qualify ? layout.ValidatorNamespace(config, t.Schema) + "." : string.Empty;
                if (registerCreate)
                {
                    sb.AppendLine($"builder.Services.AddScoped<IValidator<{dtoPrefix}Create{t.EntityName}Dto>, {valPrefix}Create{t.EntityName}DtoValidator>();");
                }
                if (registerUpdate)
                {
                    sb.AppendLine($"builder.Services.AddScoped<IValidator<{dtoPrefix}Update{t.EntityName}Dto>, {valPrefix}Update{t.EntityName}DtoValidator>();");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("if (app.Environment.IsDevelopment())");
        sb.AppendLine("{");
        sb.AppendLine("    app.MapOpenApi();");
        sb.AppendLine("    app.MapScalarApiReference();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.UseHttpsRedirection();");

        if (config.Versioning is VersioningStyle.Header or VersioningStyle.QueryString)
        {
            sb.AppendLine("app.UseMiddleware<ApiVersionMiddleware>();");
        }

        foreach (var line in AuthEmitter.PipelineUsage(config))
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();

        if (config.EndpointStyle is EndpointStyle.Controllers)
        {
            sb.AppendLine("app.MapControllers();");
        }
        else
        {
            foreach (var t in named.Tables)
            {
                sb.AppendLine($"app.Map{t.CollectionName}Endpoints();");
            }
        }

        sb.AppendLine();
        sb.AppendLine("app.Run();");
        sb.AppendLine();
        sb.AppendLine("public partial class Program { }");

        return new EmittedFile(layout.ProgramPath(config), sb.ToString());
    }

    /// <summary>Tables that get a pair of validators — write-enabled, non-view, with a primary key.</summary>
    private static IEnumerable<NamedTable> ValidatedTables(ApiSmithConfig config, NamedSchemaModel named)
    {
        var crud = config.Crud;
        var anyWrite = (crud & (CrudOperations.Post | CrudOperations.Put | CrudOperations.Patch)) != 0;
        if (!anyWrite)
        {
            yield break;
        }

        foreach (var table in named.Tables)
        {
            if (table.IsView || table.PrimaryKey is null)
            {
                continue;
            }
            yield return table;
        }
    }
}
