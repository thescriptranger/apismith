namespace ApiSmith.Config;

/// <summary>Hand-rolled parser for <c>apismith.yaml</c>; accepts only the subset <see cref="YamlWriter"/> emits.</summary>
public static class YamlReader
{
    public static ApiSmithConfig Read(string yaml)
    {
        var config = new ApiSmithConfig();
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        var crudExplicit = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var stripped = StripComment(raw);
            if (string.IsNullOrWhiteSpace(stripped))
            {
                continue;
            }

            if (IsIndented(stripped))
            {
                throw new YamlException($"Unexpected indented content on line {i + 1}; block lists are opened by their parent key.");
            }

            var colon = stripped.IndexOf(':');
            if (colon < 0)
            {
                throw new YamlException($"Line {i + 1}: expected 'key: value'.");
            }

            var key = stripped[..colon].Trim();
            var rest = stripped[(colon + 1)..].Trim();

            if (rest.Length == 0)
            {
                var items = CollectListItems(lines, ref i);
                ApplyList(config, key, items, ref crudExplicit);
                continue;
            }

            if (rest == "[]")
            {
                ApplyList(config, key, System.Array.Empty<string>(), ref crudExplicit);
                continue;
            }

            ApplyScalar(config, key, Unquote(rest), ref crudExplicit);
        }

        return config;
    }

    private static string StripComment(string line)
    {
        var inQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuote = !inQuote;
            }
            else if (c == '#' && !inQuote)
            {
                return line[..i];
            }
        }

        return line;
    }

    private static bool IsIndented(string line) =>
        line.Length > 0 && (line[0] == ' ' || line[0] == '\t');

    private static List<string> CollectListItems(string[] lines, ref int i)
    {
        var items = new List<string>();
        while (i + 1 < lines.Length)
        {
            var next = StripComment(lines[i + 1]);
            if (string.IsNullOrWhiteSpace(next))
            {
                i++;
                continue;
            }

            var trimmed = next.TrimStart();
            if (!trimmed.StartsWith("- ", System.StringComparison.Ordinal) && trimmed != "-")
            {
                break;
            }

            var value = trimmed.Length >= 2 ? trimmed[2..].Trim() : string.Empty;
            items.Add(Unquote(value));
            i++;
        }

        return items;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            var inner = value[1..^1];
            return inner.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        return value;
    }

    private static void ApplyScalar(ApiSmithConfig config, string key, string value, ref bool crudExplicit)
    {
        switch (key)
        {
            case "apiVersion":
                config.ApiVersion = value switch
                {
                    "v1" => ApiVersion.V1,
                    "v2" => ApiVersion.V2,
                    _ => throw new YamlException($"Unknown apiVersion '{value}'. Supported values: v1, v2."),
                };
                break;
            case "projectName":
                config.ProjectName = value;
                break;
            case "outputDirectory":
                config.OutputDirectory = value;
                break;
            case "targetFramework":
                config.TargetFramework = value;
                break;
            case "endpointStyle":
                config.EndpointStyle = ParseEnum<EndpointStyle>(key, value);
                break;
            case "architecture":
                config.Architecture = ParseEnum<ArchitectureStyle>(key, value);
                break;
            case "dataAccess":
                config.DataAccess = ParseEnum<DataAccessStyle>(key, value);
                break;
            case "auth":
                config.Auth = ParseEnum<AuthStyle>(key, value);
                break;
            case "versioning":
                config.Versioning = ParseEnum<VersioningStyle>(key, value);
                break;
            case "generateInitialMigration":
                config.GenerateInitialMigration = ParseBool(key, value);
                break;
            case "includeTestsProject":
                config.IncludeTestsProject = ParseBool(key, value);
                break;
            case "includeDockerAssets":
                config.IncludeDockerAssets = ParseBool(key, value);
                break;
            case "validateForeignKeyReferences":
                config.ValidateForeignKeyReferences = ParseBool(key, value);
                break;
            case "emitRepositoryInterfaces":
                config.EmitRepositoryInterfaces = ParseBool(key, value);
                break;
            case "partitionStoredProceduresBySchema":
                config.PartitionStoredProceduresBySchema = ParseBool(key, value);
                break;
            case "crud":
                throw new YamlException("'crud' must be a list.");
            case "schemas":
                config.Schemas = new List<string> { value };
                break;
            case "connectionString":
                // accepted but never round-tripped by YamlWriter
                config.ConnectionString = value;
                break;
            default:
                // forward-compat: ignore unknown keys
                break;
        }
    }

    private static void ApplyList(ApiSmithConfig config, string key, IReadOnlyList<string> items, ref bool crudExplicit)
    {
        switch (key)
        {
            case "schemas":
                config.Schemas = items.ToList();
                break;
            case "crud":
                var ops = CrudOperations.None;
                foreach (var item in items)
                {
                    ops |= ParseEnum<CrudOperations>("crud", item);
                }
                config.Crud = ops;
                crudExplicit = true;
                break;
            default:
                break;
        }
    }

    private static TEnum ParseEnum<TEnum>(string key, string value) where TEnum : struct
    {
        if (System.Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        throw new YamlException($"'{value}' is not a valid value for '{key}'.");
    }

    private static bool ParseBool(string key, string value) => value.ToLowerInvariant() switch
    {
        "true" or "yes" or "1"  => true,
        "false" or "no" or "0"  => false,
        _ => throw new YamlException($"'{value}' is not a valid boolean for '{key}'."),
    };
}

public sealed class YamlException : System.Exception
{
    public YamlException(string message) : base(message) { }
}
