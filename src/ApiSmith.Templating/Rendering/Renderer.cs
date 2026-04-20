using System.Collections;
using System.Globalization;
using System.Text;
using ApiSmith.Templating.Parsing;

namespace ApiSmith.Templating.Rendering;

internal static class Renderer
{
    public static void Render(TemplateAst ast, TemplateContext ctx, ITemplateSource sources, StringBuilder sb, int includeDepth)
    {
        if (includeDepth > 16)
        {
            throw new TemplateException($"Template include depth exceeded 16 (circular include involving '{ast.Name}'?).");
        }

        foreach (var node in ast.Nodes)
        {
            RenderNode(ast.Name, node, ctx, sources, sb, includeDepth);
        }
    }

    private static void RenderNode(string templateName, TemplateNode node, TemplateContext ctx, ITemplateSource sources, StringBuilder sb, int includeDepth)
    {
        switch (node)
        {
            case TextNode t:
                sb.Append(t.Text);
                break;

            case RawNode r:
                sb.Append(r.Text);
                break;

            case ExpressionNode e:
                var value = ctx.Resolve(e.Path);
                var text = FormatValue(value);
                foreach (var filter in e.Filters)
                {
                    text = Filters.Apply(templateName, e.Line, e.Column, text, filter);
                }
                sb.Append(text);
                break;

            case IfNode ifn:
                var cond = ctx.Resolve(ifn.ConditionPath);
                if (IsTruthy(cond))
                {
                    foreach (var child in ifn.Body)
                    {
                        RenderNode(templateName, child, ctx, sources, sb, includeDepth);
                    }
                }
                else
                {
                    foreach (var child in ifn.ElseBody)
                    {
                        RenderNode(templateName, child, ctx, sources, sb, includeDepth);
                    }
                }
                break;

            case ForNode forn:
                var collection = ctx.Resolve(forn.CollectionPath);
                if (collection is IEnumerable seq && collection is not string)
                {
                    foreach (var item in seq)
                    {
                        var child = ctx.Push();
                        child.Set(forn.IteratorName, item);
                        foreach (var childNode in forn.Body)
                        {
                            RenderNode(templateName, childNode, child, sources, sb, includeDepth);
                        }
                    }
                }
                break;

            case IncludeNode inc:
                if (!sources.TryLoad(inc.Path, out var included))
                {
                    throw TemplateException.At(templateName, inc.Line, inc.Column, $"Cannot resolve include '{inc.Path}'.");
                }

                var nestedAst = TemplateParser.Parse(inc.Path, included);
                Render(nestedAst, ctx, sources, sb, includeDepth + 1);
                break;

            default:
                throw new TemplateException($"{templateName}: unsupported AST node '{node.GetType().Name}'.");
        }
    }

    private static string FormatValue(object? value) => value switch
    {
        null          => string.Empty,
        string s      => s,
        bool b        => b ? "true" : "false",
        System.IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _             => value.ToString() ?? string.Empty,
    };

    private static bool IsTruthy(object? value) => value switch
    {
        null                 => false,
        bool b               => b,
        string s             => s.Length > 0,
        System.Collections.ICollection c => c.Count > 0,
        IEnumerable ie       => HasAny(ie),
        _                    => true,
    };

    private static bool HasAny(IEnumerable ie)
    {
        var e = ie.GetEnumerator();
        try
        {
            return e.MoveNext();
        }
        finally
        {
            if (e is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
