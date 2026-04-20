using ApiSmith.Templating;

namespace ApiSmith.UnitTests.Templating;

public sealed class TemplateEngineTests
{
    [Fact]
    public void Substitution_reads_root_property()
    {
        var engine = BuildEngine(("t", "Hi {{ Name }}."));
        var result = engine.Render("t", new { Name = "World" });
        Assert.Equal("Hi World.", result);
    }

    [Fact]
    public void Substitution_walks_dotted_path()
    {
        var engine = BuildEngine(("t", "{{ user.email }}"));
        var result = engine.Render("t", new { user = new { email = "a@b.com" } });
        Assert.Equal("a@b.com", result);
    }

    [Theory]
    [InlineData("{{ name | pascal }}", "order_items", "OrderItems")]
    [InlineData("{{ name | camel }}", "order_items", "orderItems")]
    [InlineData("{{ name | plural }}", "User", "Users")]
    [InlineData("{{ name | singular }}", "Users", "User")]
    [InlineData("{{ name | upper }}", "hello", "HELLO")]
    [InlineData("{{ name | lower }}", "HELLO", "hello")]
    [InlineData("{{ name | pascal | plural }}", "order_item", "OrderItems")]
    public void Filters_apply_in_order(string template, string input, string expected)
    {
        var engine = BuildEngine(("t", template));
        Assert.Equal(expected, engine.Render("t", new { name = input }));
    }

    [Fact]
    public void Unknown_filter_throws_with_location()
    {
        var engine = BuildEngine(("t", "{{ name | nope }}"));
        var ex = Assert.Throws<TemplateException>(() => engine.Render("t", new { name = "x" }));
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void If_truthy_renders_body()
    {
        var engine = BuildEngine(("t", "{{# if show }}YES{{/if}}"));
        Assert.Equal("YES", engine.Render("t", new { show = true }));
    }

    [Fact]
    public void If_falsy_renders_else()
    {
        var engine = BuildEngine(("t", "{{# if show }}Y{{# else }}N{{/if}}"));
        Assert.Equal("N", engine.Render("t", new { show = false }));
    }

    [Fact]
    public void Empty_string_is_falsy()
    {
        var engine = BuildEngine(("t", "{{# if s }}Y{{# else }}N{{/if}}"));
        Assert.Equal("N", engine.Render("t", new { s = "" }));
        Assert.Equal("Y", engine.Render("t", new { s = "x" }));
    }

    [Fact]
    public void Empty_collection_is_falsy()
    {
        var engine = BuildEngine(("t", "{{# if items }}Y{{# else }}N{{/if}}"));
        Assert.Equal("N", engine.Render("t", new { items = System.Array.Empty<string>() }));
        Assert.Equal("Y", engine.Render("t", new { items = new[] { "a" } }));
    }

    [Fact]
    public void For_iterates_collection()
    {
        var engine = BuildEngine(("t", "{{# for x in items }}<{{ x }}>{{/for}}"));
        var result = engine.Render("t", new { items = new[] { "a", "b", "c" } });
        Assert.Equal("<a><b><c>", result);
    }

    [Fact]
    public void For_variable_shadows_root()
    {
        var engine = BuildEngine(("t", "{{# for x in items }}{{ x.name }}{{/for}}"));
        var items = new[] { new { name = "A" }, new { name = "B" } };
        Assert.Equal("AB", engine.Render("t", new { items }));
    }

    [Fact]
    public void Include_nests_another_template()
    {
        var engine = BuildEngine(
            ("Outer", "Start-{{# include \"Partial\" }}-End"),
            ("Partial", "[{{ name }}]"));
        Assert.Equal("Start-[n]-End", engine.Render("Outer", new { name = "n" }));
    }

    [Fact]
    public void Raw_block_is_not_parsed()
    {
        var engine = BuildEngine(("t", "{{# raw }}{{ not an expression }}{{/raw}}"));
        Assert.Equal("{{ not an expression }}", engine.Render("t", new { }));
    }

    [Fact]
    public void Missing_close_tag_throws()
    {
        var engine = BuildEngine(("t", "{{# if x }}no end"));
        Assert.Throws<TemplateException>(() => engine.Render("t", new { x = true }));
    }

    [Fact]
    public void Unknown_directive_throws()
    {
        var engine = BuildEngine(("t", "{{# wat }}"));
        var ex = Assert.Throws<TemplateException>(() => engine.Render("t", new { }));
        Assert.Contains("wat", ex.Message);
    }

    [Fact]
    public void Missing_template_throws()
    {
        var engine = BuildEngine();
        Assert.Throws<TemplateException>(() => engine.Render("nope", new { }));
    }

    [Fact]
    public void Null_value_renders_as_empty_string()
    {
        var engine = BuildEngine(("t", "[{{ missing }}]"));
        Assert.Equal("[]", engine.Render("t", new { }));
    }

    [Fact]
    public void Catalog_resolves_embedded_template()
    {
        var engine = new TemplateEngine(new Templates.TemplateCatalog());
        Assert.Equal("Hello, World!\n", engine.Render("Smoke/Hello.apismith", new { name = "world" }));
    }

    private static TemplateEngine BuildEngine(params (string Name, string Source)[] templates)
    {
        var source = new InMemoryTemplateSource();
        foreach (var (name, body) in templates)
        {
            source.Add(name, body);
        }
        return new TemplateEngine(source);
    }
}
