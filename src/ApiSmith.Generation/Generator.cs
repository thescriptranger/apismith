using System.Collections.Immutable;
using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.Emitters;
using ApiSmith.Generation.IO;

namespace ApiSmith.Generation;

public sealed class Generator
{
    private readonly IScaffoldLog _log;

    public Generator(IScaffoldLog log)
    {
        _log = log;
    }

    public ScaffoldReport Generate(ApiSmithConfig config, SchemaGraph schema, string outputDirectory)
    {
        var layout = LayoutFactory.Create(config.Architecture);

        var totalSw = Stopwatch.StartNew();

        var planSw = Stopwatch.StartNew();
        var named = NamedSchemaModel.Build(schema);
        planSw.Stop();

        if (named.Tables.IsDefaultOrEmpty)
        {
            _log.Warn("No tables found in the introspected schema. Output will compile but be empty.");
        }

        var renderSw = Stopwatch.StartNew();
        var files = RenderAll(config, layout, schema, named, _log).ToImmutableArray();
        renderSw.Stop();

        var writeSw = Stopwatch.StartNew();
        FileWriter.Write(outputDirectory, files);
        writeSw.Stop();

        totalSw.Stop();

        var report = new ScaffoldReport(
            FileCount: files.Length,
            TableCount: named.Tables.Length,
            IntrospectDuration: System.TimeSpan.Zero,
            PlanDuration: planSw.Elapsed,
            RenderDuration: renderSw.Elapsed,
            WriteDuration: writeSw.Elapsed,
            TotalDuration: totalSw.Elapsed);

        _log.Info($"Scaffolded {report.FileCount} files for {report.TableCount} tables "
                  + $"({config.Architecture}/{config.EndpointStyle}/{config.DataAccess}) "
                  + $"in {report.TotalDuration.TotalMilliseconds:F0} ms.");
        return report;
    }

    private static IEnumerable<EmittedFile> RenderAll(
        ApiSmithConfig config,
        IArchitectureLayout layout,
        SchemaGraph schema,
        NamedSchemaModel named,
        IScaffoldLog log)
    {
        // Shared across emitters so `_db.{Accessor}` matches the DbContext; recomputing per-emitter would drift.
        var collidedEntityNames = DbSetNaming.CollidedEntityNames(named);


        yield return SlnEmitter.Emit(config, layout);

        foreach (var csproj in CsProjEmitter.Emit(config, layout))
        {
            yield return csproj;
        }

        yield return ProgramCsEmitter.Emit(config, layout, named, schema);
        yield return ValidationResultEmitter.Emit(config, layout);
        if (ValidationErrorEmitter.Emit(config, layout) is { } vee) yield return vee;
        if (ApiProblemEmitter.Emit(config, layout) is { } apee) yield return apee;
        if (PagedResponseEmitter.Emit(config, layout) is { } pr) yield return pr;
        foreach (var f in RequestEmitter.Emit(config, layout, named))
        {
            yield return f;
        }
        foreach (var f in ResponseEmitter.Emit(config, layout, named))
        {
            yield return f;
        }
        yield return ApiSmithConfigEmitter.Emit(config, layout);

        foreach (var file in AppSettingsEmitter.Emit(config, layout))
        {
            yield return file;
        }
        yield return LaunchSettingsEmitter.Emit(config, layout);

        foreach (var file in RepoHygieneEmitter.Emit(config, layout))
        {
            yield return file;
        }

        if (config.DataAccess is DataAccessStyle.EfCore)
        {
            yield return DbContextEmitter.Emit(config, layout, named, collidedEntityNames);
        }
        else
        {
            yield return DapperConnectionFactoryEmitter.Emit(config, layout);
        }

        if (config.Architecture is ArchitectureStyle.VerticalSlice)
        {
            foreach (var file in DispatcherEmitter.Emit(config, layout))
            {
                yield return file;
            }
        }

        foreach (var file in VersioningEmitter.Emit(config, layout))
        {
            yield return file;
        }

        foreach (var file in AuthEmitter.Emit(config, layout))
        {
            yield return file;
        }

        foreach (var file in StoredProceduresEmitter.Emit(config, layout, schema, log))
        {
            yield return file;
        }

        foreach (var file in DbFunctionsEmitter.Emit(config, layout, schema))
        {
            yield return file;
        }

        foreach (var file in MigrationsEmitter.Emit(config, layout))
        {
            yield return file;
        }

        if (config.IncludeDockerAssets)
        {
            foreach (var file in DockerEmitter.Emit(config, layout))
            {
                yield return file;
            }
        }

        foreach (var table in named.JoinTables)
        {
            yield return EntityEmitter.Emit(config, layout, named, table);
        }

        foreach (var file in EnumEmitter.Emit(config, layout, named))
        {
            yield return file;
        }

        foreach (var table in named.Tables)
        {
            yield return EntityEmitter.Emit(config, layout, named, table);
            yield return DtoEmitter.Emit(config, layout, table);
            yield return MapperEmitter.Emit(config, layout, table);

            // Views are read-only; read-only controller path relies on null PrimaryKey.
            if (!table.IsView)
            {
                yield return ValidatorEmitter.Emit(config, layout, table);
            }

            if (config.DataAccess is DataAccessStyle.Dapper)
            {
                yield return DapperRepositoryEmitter.Emit(config, layout, table);
            }

            if (config.EndpointStyle is EndpointStyle.Controllers)
            {
                yield return ControllerEmitter.Emit(config, layout, table, collidedEntityNames);
            }
            else
            {
                yield return MinimalApiEndpointEmitter.Emit(config, layout, table, collidedEntityNames);
            }
        }

        if (config.IncludeTestsProject)
        {
            foreach (var file in TestsProjectEmitter.Emit(config, layout, named))
            {
                yield return file;
            }
        }
    }
}
