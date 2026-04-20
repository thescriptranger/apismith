using ApiSmith.Core.Model;
using ApiSmith.Introspection.Readers;

namespace ApiSmith.Introspection.Tests;

public sealed class JoinTableDetectorTests
{
    [Fact]
    public void Pure_two_fk_join_table_is_detected()
    {
        var table = Table.Create("dbo", "post_tags",
            new[]
            {
                Col("post_id", 1, nullable: false),
                Col("tag_id", 2, nullable: false),
            },
            PrimaryKey.Create("PK_post_tags", new[] { "post_id", "tag_id" }));

        var fks = new[]
        {
            ForeignKey.Create("FK_post_tags_post", "dbo", "post_tags", new[] { "post_id" }, "dbo", "posts", new[] { "id" }),
            ForeignKey.Create("FK_post_tags_tag",  "dbo", "post_tags", new[] { "tag_id" },  "dbo", "tags",  new[] { "id" }),
        };

        Assert.True(JoinTableDetector.IsJoinTable(table, fks));
    }

    [Fact]
    public void Audit_columns_do_not_disqualify_a_join_table()
    {
        var table = Table.Create("dbo", "post_tags",
            new[]
            {
                Col("post_id", 1, nullable: false),
                Col("tag_id", 2, nullable: false),
                Col("created_at", 3, nullable: false),
                Col("created_by", 4, nullable: true),
            },
            PrimaryKey.Create("PK_post_tags", new[] { "post_id", "tag_id" }));

        var fks = new[]
        {
            ForeignKey.Create("FK_a", "dbo", "post_tags", new[] { "post_id" }, "dbo", "posts", new[] { "id" }),
            ForeignKey.Create("FK_b", "dbo", "post_tags", new[] { "tag_id" },  "dbo", "tags",  new[] { "id" }),
        };

        Assert.True(JoinTableDetector.IsJoinTable(table, fks));
    }

    [Fact]
    public void Meaningful_extra_column_disqualifies()
    {
        var table = Table.Create("dbo", "post_tags",
            new[]
            {
                Col("post_id", 1, nullable: false),
                Col("tag_id", 2, nullable: false),
                Col("weight", 3, nullable: false),
            },
            PrimaryKey.Create("PK_post_tags", new[] { "post_id", "tag_id" }));

        var fks = new[]
        {
            ForeignKey.Create("FK_a", "dbo", "post_tags", new[] { "post_id" }, "dbo", "posts", new[] { "id" }),
            ForeignKey.Create("FK_b", "dbo", "post_tags", new[] { "tag_id" },  "dbo", "tags",  new[] { "id" }),
        };

        Assert.False(JoinTableDetector.IsJoinTable(table, fks));
    }

    [Fact]
    public void Single_fk_is_not_a_join_table()
    {
        var table = Table.Create("dbo", "posts",
            new[]
            {
                Col("id", 1, nullable: false, identity: true),
                Col("user_id", 2, nullable: false),
            },
            PrimaryKey.Create("PK_posts", new[] { "id" }));

        var fks = new[]
        {
            ForeignKey.Create("FK_a", "dbo", "posts", new[] { "user_id" }, "dbo", "users", new[] { "id" }),
        };

        Assert.False(JoinTableDetector.IsJoinTable(table, fks));
    }

    [Fact]
    public void Missing_pk_is_not_a_join_table()
    {
        var table = Table.Create("dbo", "post_tags",
            new[]
            {
                Col("post_id", 1, nullable: false),
                Col("tag_id", 2, nullable: false),
            },
            primaryKey: null);

        var fks = new[]
        {
            ForeignKey.Create("FK_a", "dbo", "post_tags", new[] { "post_id" }, "dbo", "posts", new[] { "id" }),
            ForeignKey.Create("FK_b", "dbo", "post_tags", new[] { "tag_id" },  "dbo", "tags",  new[] { "id" }),
        };

        Assert.False(JoinTableDetector.IsJoinTable(table, fks));
    }

    private static Column Col(string name, int pos, bool nullable = false, bool identity = false) =>
        new(Name: name, OrdinalPosition: pos, SqlType: "int",
            IsNullable: nullable, IsIdentity: identity, IsComputed: false,
            MaxLength: null, Precision: null, Scale: null, DefaultValue: null);
}
