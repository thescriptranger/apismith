using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class SchemaPartitionedServicesTests
{
    [Fact]
    public void Flag_off_emits_single_istoredprocedures_interface()
    {
        var (config, output) = Setup("Sproc1");
        config.PartitionStoredProceduresBySchema = false;
        var graph = BuildGraphWithSprocsAcrossSchemas();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Sproc1", "Data", "StoredProcedures.cs");
            var content = File.ReadAllText(path);
            Assert.Contains("public interface IStoredProcedures", content);
            Assert.DoesNotContain("IDboStoredProcedures", content);
            Assert.DoesNotContain("IAuditStoredProcedures", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Flag_on_emits_per_schema_interface()
    {
        var (config, output) = Setup("Sproc2");
        config.PartitionStoredProceduresBySchema = true;
        var graph = BuildGraphWithSprocsAcrossSchemas();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dboPath = Path.Combine(output, "src", "Sproc2", "Data", "DboStoredProcedures.cs");
            var auditPath = Path.Combine(output, "src", "Sproc2", "Data", "AuditStoredProcedures.cs");
            Assert.True(File.Exists(dboPath), $"Missing {dboPath}");
            Assert.True(File.Exists(auditPath), $"Missing {auditPath}");

            var dbo = File.ReadAllText(dboPath);
            var audit = File.ReadAllText(auditPath);
            Assert.Contains("public interface IDboStoredProcedures", dbo);
            Assert.Contains("public interface IAuditStoredProcedures", audit);
            // Each interface has only its own schema's methods
            Assert.Contains("GetUsers", dbo);       // dbo sproc
            Assert.DoesNotContain("GetUsers", audit);
            Assert.Contains("GetAuditLog", audit);  // audit sproc
            Assert.DoesNotContain("GetAuditLog", dbo);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Flag_on_program_cs_registers_per_schema_bindings()
    {
        var (config, output) = Setup("Sproc3");
        config.PartitionStoredProceduresBySchema = true;
        var graph = BuildGraphWithSprocsAcrossSchemas();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var program = File.ReadAllText(Path.Combine(output, "src", "Sproc3", "Program.cs"));
            Assert.Contains("AddScoped<IDboStoredProcedures, DboStoredProcedures>()", program);
            Assert.Contains("AddScoped<IAuditStoredProcedures, AuditStoredProcedures>()", program);
            Assert.DoesNotContain("AddScoped<IStoredProcedures,", program);
        }
        finally { CleanupBestEffort(output); }
    }

    private static SchemaGraph BuildGraphWithSprocsAcrossSchemas()
    {
        // Sproc in dbo schema — no parameters, no result columns.
        var sprocDbo = StoredProcedure.Create(
            schema: "dbo",
            name: "get_users",
            parameters: System.Array.Empty<SprocParameter>(),
            resultColumns: System.Array.Empty<ResultColumn>());

        // Sproc in audit schema — also minimal.
        var sprocAudit = StoredProcedure.Create(
            schema: "audit",
            name: "get_audit_log",
            parameters: System.Array.Empty<SprocParameter>(),
            resultColumns: System.Array.Empty<ResultColumn>());

        // Need at least one table per schema so downstream emitters don't choke; use an empty table list is fine because
        // DbSchema.Create accepts any table list. But entity/DbContext emitters rely on tables; using empties is safe here.
        var dboSchema = DbSchema.Create(
            name: "dbo",
            tables: System.Array.Empty<Table>(),
            procedures: new[] { sprocDbo });

        var auditSchema = DbSchema.Create(
            name: "audit",
            tables: System.Array.Empty<Table>(),
            procedures: new[] { sprocAudit });

        return SchemaGraph.Create(new[] { dboSchema, auditSchema });
    }

    private static (ApiSmithConfig Config, string Output) Setup(string projectName)
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests", projectName + "-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = projectName,
            OutputDirectory = output,
            ConnectionString = "Server=test;Database=test;Trusted_Connection=True;",
        };
        return (config, output);
    }

    private static void CleanupBestEffort(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Don't fail the test because of cleanup; CI temp dirs get reaped anyway.
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
