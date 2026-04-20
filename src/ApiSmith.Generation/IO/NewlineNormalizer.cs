namespace ApiSmith.Generation.IO;

public static class NewlineNormalizer
{
    /// <summary>LF + trailing newline; byte-identical replay depends on this being host-OS-independent.</summary>
    public static string ToLf(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }
}
