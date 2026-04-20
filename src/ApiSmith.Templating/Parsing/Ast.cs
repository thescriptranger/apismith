using System.Collections.Immutable;

namespace ApiSmith.Templating.Parsing;

internal abstract record TemplateNode;

internal sealed record TextNode(string Text) : TemplateNode;

internal sealed record ExpressionNode(string Path, ImmutableArray<string> Filters, int Line, int Column) : TemplateNode;

internal sealed record IfNode(
    string ConditionPath,
    ImmutableArray<TemplateNode> Body,
    ImmutableArray<TemplateNode> ElseBody,
    int Line,
    int Column) : TemplateNode;

internal sealed record ForNode(
    string IteratorName,
    string CollectionPath,
    ImmutableArray<TemplateNode> Body,
    int Line,
    int Column) : TemplateNode;

internal sealed record IncludeNode(string Path, int Line, int Column) : TemplateNode;

internal sealed record RawNode(string Text) : TemplateNode;

internal sealed record TemplateAst(string Name, ImmutableArray<TemplateNode> Nodes);
