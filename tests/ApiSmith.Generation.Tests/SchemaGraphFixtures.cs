using ApiSmith.Core.Model;
using ApiSmith.Introspection;

namespace ApiSmith.Generation.Tests;

/// <summary>Hand-built in-memory schemas so generator tests don't need a live SQL Server.</summary>
internal static class SchemaGraphFixtures
{
    public static SchemaGraph SmallBlog()
    {
        var users = Table.Create("dbo", "users",
            new[]
            {
                new Column("id",         1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email",      2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256,  Precision: null, Scale: null, DefaultValue: null),
                new Column("name",       3, "nvarchar", IsNullable: true,  IsIdentity: false, IsComputed: false, MaxLength: 100,  Precision: null, Scale: null, DefaultValue: null),
                new Column("created_at", 4, "datetime2",IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_users", new[] { "id" }));

        var posts = Table.Create("dbo", "posts",
            new[]
            {
                new Column("id",        1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("user_id",   2, "int",      IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("title",     3, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 200,  Precision: null, Scale: null, DefaultValue: null),
                new Column("body",      4, "nvarchar", IsNullable: true,  IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("published", 5, "bit",      IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: "((0))"),
            },
            PrimaryKey.Create("PK_posts", new[] { "id" }));

        var auditLog = Table.Create("dbo", "audit_log",
            new[]
            {
                new Column("event_id",  1, "uniqueidentifier", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("entity",    2, "nvarchar",         IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 50,   Precision: null, Scale: null, DefaultValue: null),
                new Column("payload",   3, "nvarchar",         IsNullable: true,  IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("when_utc",  4, "datetime2",        IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_audit_log", new[] { "event_id" }));

        var dbo = DbSchema.Create("dbo", new[] { users, posts, auditLog });
        return SchemaGraph.Create(new[] { dbo });
    }

    /// <summary>Single table with a uniqueidentifier PK seeded via DEFAULT NEWID() — the shape that exposed the identity-only skip bug.</summary>
    public static SchemaGraph GuidPkEntity()
    {
        var genders = Table.Create("dbo", "genders",
            new[]
            {
                new Column("gender_id", 1, "uniqueidentifier", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: "(newid())"),
                new Column("name",      2, "nvarchar",         IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_genders", new[] { "gender_id" }));

        var dbo = DbSchema.Create("dbo", new[] { genders });
        return SchemaGraph.Create(new[] { dbo });
    }

    /// <summary>Table with two self-referencing FKs — the shape that exposed the multi-FK WithMany bug.</summary>
    public static SchemaGraph SelfReferencingWithTwoFks()
    {
        var billingProfiles = Table.Create("dbo", "billing_profiles",
            new[]
            {
                new Column("id",                         1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("name",                       2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100,  Precision: null, Scale: null, DefaultValue: null),
                new Column("charges_bill_to_profile_id", 3, "int",      IsNullable: true,  IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("dues_bill_to_profile_id",    4, "int",      IsNullable: true,  IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_billing_profiles", new[] { "id" }),
            foreignKeys: new[]
            {
                ForeignKey.Create("FK_bp_charges", "dbo", "billing_profiles", new[] { "charges_bill_to_profile_id" }, "dbo", "billing_profiles", new[] { "id" }),
                ForeignKey.Create("FK_bp_dues",    "dbo", "billing_profiles", new[] { "dues_bill_to_profile_id" },    "dbo", "billing_profiles", new[] { "id" }),
            });

        var dbo = DbSchema.Create("dbo", new[] { billingProfiles });
        return SchemaGraph.Create(new[] { dbo });
    }

    /// <summary>One-to-many (user→posts) plus many-to-many via post_tags join table.</summary>
    public static SchemaGraph Relational()
    {
        var users = Table.Create("dbo", "users",
            new[]
            {
                new Column("id",    1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_users", new[] { "id" }));

        var posts = Table.Create("dbo", "posts",
            new[]
            {
                new Column("id",      1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("user_id", 2, "int",      IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("title",   3, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 200,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_posts", new[] { "id" }));

        var tags = Table.Create("dbo", "tags",
            new[]
            {
                new Column("id",   1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("name", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 50,   Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_tags", new[] { "id" }));

        var postTags = Table.Create("dbo", "post_tags",
            new[]
            {
                new Column("post_id", 1, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("tag_id",  2, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_post_tags", new[] { "post_id", "tag_id" }));

        var fks = new[]
        {
            ForeignKey.Create("FK_posts_users",  "dbo", "posts",     new[] { "user_id" }, "dbo", "users", new[] { "id" }),
            ForeignKey.Create("FK_pt_posts",     "dbo", "post_tags", new[] { "post_id" }, "dbo", "posts", new[] { "id" }),
            ForeignKey.Create("FK_pt_tags",      "dbo", "post_tags", new[] { "tag_id" },  "dbo", "tags",  new[] { "id" }),
        };

        return SqlServerSchemaReader.BuildGraph(
            new[] { users, posts, tags, postTags },
            fks,
            System.Array.Empty<View>(),
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());
    }

    /// <summary>Same tables as <see cref="Relational"/> plus an orders table with a translatable CHECK on total_cents &gt;= 0.</summary>
    public static SchemaGraph RelationalWithCheck()
    {
        var users = Table.Create("dbo", "users",
            new[]
            {
                new Column("id",    1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_users", new[] { "id" }));

        var orders = Table.Create("dbo", "orders",
            columns: new[]
            {
                new Column("id",          1, "int",    IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("user_id",     2, "int",    IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("total_cents", 3, "bigint", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_orders", new[] { "id" }),
            checkConstraints: new[] { new CheckConstraint("CK_orders_total_nonneg", "([total_cents] >= 0)") });

        var fks = new[]
        {
            ForeignKey.Create("FK_orders_users", "dbo", "orders", new[] { "user_id" }, "dbo", "users", new[] { "id" }),
        };

        return SqlServerSchemaReader.BuildGraph(
            new[] { users, orders },
            fks,
            System.Array.Empty<View>(),
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());
    }

    /// <summary>audit.user_actions FK → dbo.users; drives the multi-schema folder/namespace test.</summary>
    public static SchemaGraph CrossSchema()
    {
        var user = Table.Create("dbo", "users",
            new[]
            {
                new Column("id",    1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_users", new[] { "id" }));

        var userAction = Table.Create("audit", "user_actions",
            new[]
            {
                new Column("id",      1, "bigint",   IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("user_id", 2, "int",      IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("action",  3, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_user_actions", new[] { "id" }));

        var fks = new[]
        {
            ForeignKey.Create(
                name: "FK_user_actions_users",
                fromSchema: "audit",
                fromTable: "user_actions",
                fromColumns: new[] { "user_id" },
                toSchema: "dbo",
                toTable: "users",
                toColumns: new[] { "id" }),
        };

        return SqlServerSchemaReader.BuildGraph(
            new[] { user, userAction },
            fks,
            System.Array.Empty<View>(),
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());
    }

    /// <summary>dbo and audit each own same-named users/groups + within-schema user_groups join; exercises skip-nav schema disambiguation.</summary>
    public static SchemaGraph CrossSchemaNameCollision()
    {
        var dboUser = Table.Create("dbo", "users",
            new[]
            {
                new Column("id",    1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_dbo_users", new[] { "id" }));

        var dboGroup = Table.Create("dbo", "groups",
            new[]
            {
                new Column("id",   1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("name", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 50,   Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_dbo_groups", new[] { "id" }));

        var dboUserGroups = Table.Create("dbo", "user_groups",
            new[]
            {
                new Column("user_id",  1, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("group_id", 2, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_dbo_user_groups", new[] { "user_id", "group_id" }));

        var auditUser = Table.Create("audit", "users",
            new[]
            {
                new Column("id",    1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_audit_users", new[] { "id" }));

        var auditGroup = Table.Create("audit", "groups",
            new[]
            {
                new Column("id",   1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("name", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 50,   Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_audit_groups", new[] { "id" }));

        var auditUserGroups = Table.Create("audit", "user_groups",
            new[]
            {
                new Column("user_id",  1, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("group_id", 2, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_audit_user_groups", new[] { "user_id", "group_id" }));

        var fks = new[]
        {
            ForeignKey.Create("FK_dug_user",  "dbo",   "user_groups", new[] { "user_id" },  "dbo",   "users",  new[] { "id" }),
            ForeignKey.Create("FK_dug_group", "dbo",   "user_groups", new[] { "group_id" }, "dbo",   "groups", new[] { "id" }),
            ForeignKey.Create("FK_aug_user",  "audit", "user_groups", new[] { "user_id" },  "audit", "users",  new[] { "id" }),
            ForeignKey.Create("FK_aug_group", "audit", "user_groups", new[] { "group_id" }, "audit", "groups", new[] { "id" }),
        };

        return SqlServerSchemaReader.BuildGraph(
            new[] { dboUser, dboGroup, dboUserGroups, auditUser, auditGroup, auditUserGroups },
            fks,
            System.Array.Empty<View>(),
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());
    }
}
