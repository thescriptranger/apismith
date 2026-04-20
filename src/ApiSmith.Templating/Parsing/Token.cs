namespace ApiSmith.Templating.Parsing;

internal enum TokenKind
{
    Text,
    Expression,        // {{ path [| filter]* }}
    IfStart,           // {{# if path }}
    ElseMarker,        // {{# else }}
    BlockEnd,          // {{/ name }}
    ForStart,          // {{# for var in path }}
    IncludeDirective,  // {{# include "path" }}
    RawStart,          // {{# raw }}
}

internal sealed record Token(TokenKind Kind, string Body, int Line, int Column)
{
    public override string ToString() => $"{Kind}@{Line}:{Column} {Body}";
}
