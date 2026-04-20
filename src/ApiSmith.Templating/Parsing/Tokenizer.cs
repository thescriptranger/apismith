using System.Text;

namespace ApiSmith.Templating.Parsing;

/// <summary>Single-pass scanner. Handles text, expression, if/else/end, for, include, raw.</summary>
internal static class Tokenizer
{
    public static List<Token> Tokenize(string templateName, string source)
    {
        var tokens = new List<Token>();
        var textStart = 0;
        var textStartLine = 1;
        var textStartCol = 1;
        var line = 1;
        var col = 1;
        var i = 0;

        while (i < source.Length)
        {
            if (!(i + 1 < source.Length && source[i] == '{' && source[i + 1] == '{'))
            {
                if (source[i] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }

                i++;
                continue;
            }

            // Hit "{{" — flush text, then parse the tag.
            if (i > textStart)
            {
                tokens.Add(new Token(TokenKind.Text, source[textStart..i], textStartLine, textStartCol));
            }

            var tagStartLine = line;
            var tagStartCol = col;

            i += 2;
            col += 2;

            var directiveKind = PeekDirectiveKind(source, i);

            switch (directiveKind)
            {
                case DirectiveKind.Expression:
                    i = ReadUntilCloseBraces(source, i, templateName, tagStartLine, tagStartCol, out var exprBody, ref line, ref col);
                    tokens.Add(new Token(TokenKind.Expression, exprBody.Trim(), tagStartLine, tagStartCol));
                    break;

                case DirectiveKind.BlockOpen:
                    i = ReadUntilCloseBraces(source, i, templateName, tagStartLine, tagStartCol, out var openBody, ref line, ref col);
                    var trimmed = openBody.TrimStart();
                    trimmed = trimmed[1..].TrimStart(); // drop leading '#'

                    if (StartsWithWord(trimmed, "if"))
                    {
                        tokens.Add(new Token(TokenKind.IfStart, trimmed[2..].Trim(), tagStartLine, tagStartCol));
                    }
                    else if (StartsWithWord(trimmed, "else"))
                    {
                        if (trimmed[4..].Trim().Length != 0)
                        {
                            throw TemplateException.At(templateName, tagStartLine, tagStartCol, "'else' takes no arguments.");
                        }

                        tokens.Add(new Token(TokenKind.ElseMarker, string.Empty, tagStartLine, tagStartCol));
                    }
                    else if (StartsWithWord(trimmed, "for"))
                    {
                        tokens.Add(new Token(TokenKind.ForStart, trimmed[3..].Trim(), tagStartLine, tagStartCol));
                    }
                    else if (StartsWithWord(trimmed, "include"))
                    {
                        tokens.Add(new Token(TokenKind.IncludeDirective, trimmed[7..].Trim(), tagStartLine, tagStartCol));
                    }
                    else if (StartsWithWord(trimmed, "raw"))
                    {
                        if (trimmed[3..].Trim().Length != 0)
                        {
                            throw TemplateException.At(templateName, tagStartLine, tagStartCol, "'raw' takes no arguments.");
                        }

                        i = ReadRawBody(source, i, templateName, tagStartLine, tagStartCol, out var rawBody, ref line, ref col);
                        tokens.Add(new Token(TokenKind.RawStart, rawBody, tagStartLine, tagStartCol));
                    }
                    else
                    {
                        throw TemplateException.At(templateName, tagStartLine, tagStartCol,
                            $"Unknown block directive '{trimmed}'. Expected one of: if, else, for, include, raw.");
                    }
                    break;

                case DirectiveKind.BlockClose:
                    i = ReadUntilCloseBraces(source, i, templateName, tagStartLine, tagStartCol, out var closeBody, ref line, ref col);
                    var closeTrimmed = closeBody.TrimStart();
                    tokens.Add(new Token(TokenKind.BlockEnd, closeTrimmed[1..].Trim(), tagStartLine, tagStartCol)); // drop leading '/'
                    break;

                default:
                    throw TemplateException.At(templateName, tagStartLine, tagStartCol, "Malformed '{{' tag.");
            }

            textStart = i;
            textStartLine = line;
            textStartCol = col;
        }

        if (textStart < source.Length)
        {
            tokens.Add(new Token(TokenKind.Text, source[textStart..], textStartLine, textStartCol));
        }

        return tokens;
    }

    private enum DirectiveKind
    {
        Expression,
        BlockOpen,
        BlockClose,
        Invalid,
    }

    private static DirectiveKind PeekDirectiveKind(string source, int i)
    {
        var j = i;
        while (j < source.Length && (source[j] == ' ' || source[j] == '\t'))
        {
            j++;
        }

        if (j >= source.Length)
        {
            return DirectiveKind.Invalid;
        }

        return source[j] switch
        {
            '#' => DirectiveKind.BlockOpen,
            '/' => DirectiveKind.BlockClose,
            _ => DirectiveKind.Expression,
        };
    }

    private static int ReadUntilCloseBraces(
        string source,
        int start,
        string templateName,
        int tagLine,
        int tagCol,
        out string body,
        ref int line,
        ref int col)
    {
        var sb = new StringBuilder();
        var i = start;

        while (i + 1 < source.Length)
        {
            if (source[i] == '}' && source[i + 1] == '}')
            {
                body = sb.ToString();
                col += 2;
                return i + 2;
            }

            if (source[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }

            sb.Append(source[i]);
            i++;
        }

        throw TemplateException.At(templateName, tagLine, tagCol, "Unterminated '{{' tag; missing '}}'.");
    }

    private static int ReadRawBody(
        string source,
        int start,
        string templateName,
        int tagLine,
        int tagCol,
        out string body,
        ref int line,
        ref int col)
    {
        // Scan forward for {{/ raw }}; body is everything in between verbatim.
        var i = start;
        var bodyStart = i;
        while (i < source.Length)
        {
            if (i + 1 < source.Length && source[i] == '{' && source[i + 1] == '{')
            {
                var probe = i + 2;
                while (probe < source.Length && (source[probe] == ' ' || source[probe] == '\t'))
                {
                    probe++;
                }

                if (probe < source.Length && source[probe] == '/')
                {
                    probe++;
                    while (probe < source.Length && (source[probe] == ' ' || source[probe] == '\t'))
                    {
                        probe++;
                    }

                    if (probe + 3 <= source.Length && source[probe..(probe + 3)] == "raw")
                    {
                        var after = probe + 3;
                        while (after < source.Length && (source[after] == ' ' || source[after] == '\t'))
                        {
                            after++;
                        }

                        if (after + 1 < source.Length && source[after] == '}' && source[after + 1] == '}')
                        {
                            body = source[bodyStart..i];
                            return after + 2;
                        }
                    }
                }
            }

            if (source[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }

            i++;
        }

        throw TemplateException.At(templateName, tagLine, tagCol, "Unterminated '{{# raw }}' block; missing '{{/ raw }}'.");
    }

    private static bool StartsWithWord(string text, string word)
    {
        if (!text.StartsWith(word, System.StringComparison.Ordinal))
        {
            return false;
        }

        if (text.Length == word.Length)
        {
            return true;
        }

        var next = text[word.Length];
        return next is ' ' or '\t' or '\r' or '\n';
    }
}
