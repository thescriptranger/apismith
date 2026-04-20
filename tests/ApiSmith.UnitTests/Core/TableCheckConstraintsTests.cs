using ApiSmith.Core.Model;

namespace ApiSmith.UnitTests.Core;

public sealed class TableCheckConstraintsTests
{
    [Fact]
    public void Default_table_has_empty_check_constraints()
    {
        var t = Table.Create(
            schema: "dbo",
            name: "users",
            columns: new[]
            {
                new Column("id", 1, "int", IsNullable: false, IsIdentity: true, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_users", new[] { "id" }));
        Assert.Empty(t.CheckConstraints);
    }

    [Fact]
    public void Check_constraints_are_sorted_by_name_ordinal()
    {
        var t = Table.Create(
            schema: "dbo",
            name: "users",
            columns: new[]
            {
                new Column("id", 1, "int", IsNullable: false, IsIdentity: true, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_users", new[] { "id" }),
            checkConstraints: new[]
            {
                new CheckConstraint("ck_b", "([x] > 0)"),
                new CheckConstraint("ck_a", "([y] < 100)"),
            });
        Assert.Equal(new[] { "ck_a", "ck_b" }, t.CheckConstraints.Select(c => c.Name).ToArray());
    }
}
