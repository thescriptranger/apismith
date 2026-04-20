using ApiSmith.Introspection;

namespace ApiSmith.UnitTests.Introspection;

public sealed class ConnectionValidationTests
{
    [Fact]
    public async Task Returns_invalid_when_server_does_not_exist()
    {
        // unreachable host + unused port
        var connection = "Server=tcp:nonexistent.apismith.local,1;Database=test;Encrypt=false;User Id=sa;Password=x;";
        var result = await SqlServerSchemaReader.ValidateAsync(connection, CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Returns_invalid_on_malformed_connection_string()
    {
        var result = await SqlServerSchemaReader.ValidateAsync("this-is-not-a-connection-string", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Never_throws_on_empty_connection_string()
    {
        var result = await SqlServerSchemaReader.ValidateAsync(string.Empty, CancellationToken.None);
        Assert.False(result.IsValid);
    }
}
