namespace ApiSmith.Cli.Commands;

internal static class ArgParser
{
    public static (NewCommandArgs Args, string? Error) ParseNew(System.ReadOnlySpan<string> raw)
    {
        var args = new NewCommandArgs();

        for (var i = 0; i < raw.Length; i++)
        {
            var token = raw[i];

            switch (token)
            {
                case "--config":
                    if (++i >= raw.Length)
                    {
                        return (args, "--config requires a file path.");
                    }

                    args.ConfigPath = raw[i];
                    break;

                case "--name" or "-n":
                    if (++i >= raw.Length)
                    {
                        return (args, "--name requires a value.");
                    }

                    args.Name = raw[i];
                    break;

                case "--output" or "-o":
                    if (++i >= raw.Length)
                    {
                        return (args, "--output requires a value.");
                    }

                    args.Output = raw[i];
                    break;

                case "--connection" or "-c":
                    if (++i >= raw.Length)
                    {
                        return (args, "--connection requires a value.");
                    }

                    args.ConnectionString = raw[i];
                    break;

                case "--schema" or "-s":
                    if (++i >= raw.Length)
                    {
                        return (args, "--schema requires a value.");
                    }

                    args.Schemas.Add(raw[i]);
                    break;

                default:
                    return (args, $"unknown option '{token}'.");
            }
        }

        return (args, null);
    }
}
