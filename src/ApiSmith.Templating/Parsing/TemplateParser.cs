using System.Collections.Immutable;

namespace ApiSmith.Templating.Parsing;

/// <summary>Tokens to AST; enforces <c>{{# if/for }}</c> / <c>{{/ if/for }}</c> balance.</summary>
internal static class TemplateParser
{
    public static TemplateAst Parse(string templateName, string source)
    {
        var tokens = Tokenizer.Tokenize(templateName, source);
        var index = 0;
        var nodes = ParseBlock(templateName, tokens, ref index, terminator: null);
        return new TemplateAst(templateName, nodes);
    }

    private static ImmutableArray<TemplateNode> ParseBlock(
        string templateName,
        List<Token> tokens,
        ref int i,
        string? terminator)
    {
        var builder = ImmutableArray.CreateBuilder<TemplateNode>();

        while (i < tokens.Count)
        {
            var tk = tokens[i];

            if (tk.Kind == TokenKind.BlockEnd)
            {
                if (terminator is null)
                {
                    throw TemplateException.At(templateName, tk.Line, tk.Column,
                        $"Unexpected block end '{{{{/ {tk.Body} }}}}' with no open block.");
                }

                if (!string.Equals(tk.Body, terminator, System.StringComparison.Ordinal))
                {
                    throw TemplateException.At(templateName, tk.Line, tk.Column,
                        $"Expected '{{{{/ {terminator} }}}}' but got '{{{{/ {tk.Body} }}}}'.");
                }

                i++;
                return builder.ToImmutable();
            }

            if (tk.Kind == TokenKind.ElseMarker)
            {
                // 'else' only valid inside ParseIf.
                throw TemplateException.At(templateName, tk.Line, tk.Column, "'else' without matching 'if'.");
            }

            switch (tk.Kind)
            {
                case TokenKind.Text:
                    builder.Add(new TextNode(tk.Body));
                    i++;
                    break;

                case TokenKind.Expression:
                    builder.Add(ParseExpression(tk));
                    i++;
                    break;

                case TokenKind.IfStart:
                    builder.Add(ParseIf(templateName, tokens, ref i, tk));
                    break;

                case TokenKind.ForStart:
                    builder.Add(ParseFor(templateName, tokens, ref i, tk));
                    break;

                case TokenKind.IncludeDirective:
                    builder.Add(ParseInclude(templateName, tk));
                    i++;
                    break;

                case TokenKind.RawStart:
                    builder.Add(new RawNode(tk.Body));
                    i++;
                    break;

                default:
                    throw TemplateException.At(templateName, tk.Line, tk.Column,
                        $"Unexpected token '{tk.Kind}' at this position.");
            }
        }

        if (terminator is not null)
        {
            throw new TemplateException($"{templateName}: unterminated block '{{{{# {terminator} }}}}'.");
        }

        return builder.ToImmutable();
    }

    private static ExpressionNode ParseExpression(Token tk)
    {
        var parts = tk.Body.Split('|', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw TemplateException.At(string.Empty, tk.Line, tk.Column, "Empty expression.");
        }

        return new ExpressionNode(parts[0], parts.Skip(1).ToImmutableArray(), tk.Line, tk.Column);
    }

    private static IfNode ParseIf(string templateName, List<Token> tokens, ref int i, Token ifTk)
    {
        if (string.IsNullOrWhiteSpace(ifTk.Body))
        {
            throw TemplateException.At(templateName, ifTk.Line, ifTk.Column, "'if' requires a condition path.");
        }

        i++;
        var body = ImmutableArray.CreateBuilder<TemplateNode>();
        var elseBody = ImmutableArray.CreateBuilder<TemplateNode>();
        var seenElse = false;

        while (i < tokens.Count)
        {
            var tk = tokens[i];

            if (tk.Kind == TokenKind.BlockEnd && string.Equals(tk.Body, "if", System.StringComparison.Ordinal))
            {
                i++;
                return new IfNode(ifTk.Body.Trim(), body.ToImmutable(), elseBody.ToImmutable(), ifTk.Line, ifTk.Column);
            }

            if (tk.Kind == TokenKind.ElseMarker)
            {
                if (seenElse)
                {
                    throw TemplateException.At(templateName, tk.Line, tk.Column, "Duplicate 'else' in the same 'if'.");
                }

                seenElse = true;
                i++;
                continue;
            }

            var target = seenElse ? elseBody : body;
            var parsed = ParseSingleNode(templateName, tokens, ref i);
            target.Add(parsed);
        }

        throw TemplateException.At(templateName, ifTk.Line, ifTk.Column, "Unterminated 'if' block; missing '{{/ if }}'.");
    }

    private static ForNode ParseFor(string templateName, List<Token> tokens, ref int i, Token forTk)
    {
        var parts = forTk.Body.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[1], "in", System.StringComparison.Ordinal))
        {
            throw TemplateException.At(templateName, forTk.Line, forTk.Column,
                "Expected 'for <var> in <path>'.");
        }

        var iterator = parts[0];
        var collectionPath = parts[2];
        i++;

        var body = ParseBlock(templateName, tokens, ref i, terminator: "for");
        return new ForNode(iterator, collectionPath, body, forTk.Line, forTk.Column);
    }

    private static IncludeNode ParseInclude(string templateName, Token tk)
    {
        var body = tk.Body.Trim();
        if (body.Length < 2 || body[0] != '"' || body[^1] != '"')
        {
            throw TemplateException.At(templateName, tk.Line, tk.Column,
                "'include' requires a double-quoted template path.");
        }

        return new IncludeNode(body[1..^1], tk.Line, tk.Column);
    }

    private static TemplateNode ParseSingleNode(string templateName, List<Token> tokens, ref int i)
    {
        var tk = tokens[i];
        switch (tk.Kind)
        {
            case TokenKind.Text:
                i++;
                return new TextNode(tk.Body);
            case TokenKind.Expression:
                i++;
                return ParseExpression(tk);
            case TokenKind.IfStart:
                return ParseIf(templateName, tokens, ref i, tk);
            case TokenKind.ForStart:
                return ParseFor(templateName, tokens, ref i, tk);
            case TokenKind.IncludeDirective:
                i++;
                return ParseInclude(templateName, tk);
            case TokenKind.RawStart:
                i++;
                return new RawNode(tk.Body);
            default:
                throw TemplateException.At(templateName, tk.Line, tk.Column,
                    $"Unexpected token '{tk.Kind}' here.");
        }
    }
}
