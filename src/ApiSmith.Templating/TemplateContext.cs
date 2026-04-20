using System.Collections;
using System.Reflection;

namespace ApiSmith.Templating;

/// <summary>Resolves dotted paths (<c>entity.Columns</c>) via dict key, property, then field. Scopes stack for loop vars.</summary>
public sealed class TemplateContext
{
    private readonly Dictionary<string, object?> _locals = new(System.StringComparer.Ordinal);
    private readonly TemplateContext? _parent;
    private readonly object? _root;

    public TemplateContext(object? root)
    {
        _root = root;
    }

    private TemplateContext(TemplateContext parent)
    {
        _parent = parent;
        _root = parent._root;
    }

    public TemplateContext Push()
    {
        return new TemplateContext(this);
    }

    public void Set(string name, object? value)
    {
        _locals[name] = value;
    }

    public object? Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split('.', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        object? current = ResolveRoot(segments[0]);

        for (var i = 1; i < segments.Length && current is not null; i++)
        {
            current = ResolveMember(current, segments[i]);
        }

        return current;
    }

    private object? ResolveRoot(string first)
    {
        // Loop vars shadow root members — walk locals first.
        for (var ctx = this; ctx is not null; ctx = ctx._parent)
        {
            if (ctx._locals.TryGetValue(first, out var value))
            {
                return value;
            }
        }

        if (_root is null)
        {
            return null;
        }

        return ResolveMember(_root, first);
    }

    private static object? ResolveMember(object target, string name)
    {
        if (target is IDictionary<string, object?> stringKeyed && stringKeyed.TryGetValue(name, out var dv))
        {
            return dv;
        }

        var type = target.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null)
        {
            return prop.GetValue(target);
        }

        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null)
        {
            return field.GetValue(target);
        }

        if (target is IDictionary dict && dict.Contains(name))
        {
            return dict[name];
        }

        return null;
    }
}
