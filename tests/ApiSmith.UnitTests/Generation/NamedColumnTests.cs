using ApiSmith.Core.Model;
using ApiSmith.Generation;

namespace ApiSmith.UnitTests.Generation;

public sealed class NamedColumnTests
{
    [Fact]
    public void Int_identity_pk_is_server_generated()
    {
        var table = Table.Create(
            schema: "dbo",
            name: "widgets",
            columns: new[]
            {
                new Column("widget_id", 1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("name",      2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100,  Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_widgets", new[] { "widget_id" }));

        var named = NamedTable.ShellFrom(table);

        Assert.NotNull(named.PrimaryKey);
        Assert.True(named.PrimaryKey!.IsServerGenerated);
    }

    [Fact]
    public void Guid_pk_with_default_is_server_generated()
    {
        var table = Table.Create(
            schema: "dbo",
            name: "genders",
            columns: new[]
            {
                new Column("gender_id", 1, "uniqueidentifier", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: "(newid())"),
                new Column("name",      2, "nvarchar",         IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100,  Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_genders", new[] { "gender_id" }));

        var named = NamedTable.ShellFrom(table);

        Assert.NotNull(named.PrimaryKey);
        Assert.True(named.PrimaryKey!.IsServerGenerated);
    }

    [Fact]
    public void Natural_string_pk_without_default_is_not_server_generated()
    {
        var table = Table.Create(
            schema: "dbo",
            name: "country_codes",
            columns: new[]
            {
                new Column("code",  1, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 2,   Precision: null, Scale: null, DefaultValue: null),
                new Column("label", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100, Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_country_codes", new[] { "code" }));

        var named = NamedTable.ShellFrom(table);

        Assert.NotNull(named.PrimaryKey);
        Assert.False(named.PrimaryKey!.IsServerGenerated);
    }

    [Fact]
    public void Non_pk_column_with_default_is_not_server_generated()
    {
        var table = Table.Create(
            schema: "dbo",
            name: "widgets",
            columns: new[]
            {
                new Column("widget_id",  1, "int",       IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("created_at", 2, "datetime2", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: "(getdate())"),
            },
            primaryKey: PrimaryKey.Create("PK_widgets", new[] { "widget_id" }));

        var named = NamedTable.ShellFrom(table);

        var createdAt = named.Columns.First(c => c.DbName == "created_at");
        Assert.False(createdAt.IsServerGenerated);
    }
}
