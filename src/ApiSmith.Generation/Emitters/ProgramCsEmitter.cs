using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class ProgramCsEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named)
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
                sb.AppendLine($"builder.Services.AddScoped<{t.EntityName}Repository>();");
            }
        }

        if (config.Architecture is ArchitectureStyle.VerticalSlice)
        {
            sb.AppendLine("builder.Services.AddDispatcher(typeof(Program).Assembly);");
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
}
