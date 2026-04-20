using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class DapperConnectionFactoryEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var ns = layout.DataNamespace(config);
        var content = $$"""
            using System.Data;
            using System.Data.Common;
            using Microsoft.Data.SqlClient;
            using Microsoft.Extensions.Configuration;

            namespace {{ns}};

            public interface IDbConnectionFactory
            {
                System.Threading.Tasks.Task<DbConnection> OpenAsync(System.Threading.CancellationToken ct = default);
            }

            public sealed class SqlDbConnectionFactory : IDbConnectionFactory
            {
                private readonly string _connectionString;

                public SqlDbConnectionFactory(IConfiguration configuration)
                {
                    _connectionString = configuration.GetConnectionString("DefaultConnection")
                        ?? throw new System.InvalidOperationException("Missing connection string 'DefaultConnection' in configuration.");
                }

                public async System.Threading.Tasks.Task<DbConnection> OpenAsync(System.Threading.CancellationToken ct = default)
                {
                    var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync(ct).ConfigureAwait(false);
                    return conn;
                }
            }
            """;

        return new EmittedFile(layout.ConnectionFactoryPath(config), content);
    }
}
