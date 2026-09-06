namespace FileGenerator.Cli;

public static class GeneratorCli
{
    public const string Usage =
        "Usage: FileGenerator --output <path> --size <bytes|KB|MB|GB> [--seed <int>] [--unique-strings <n>] [--max-number <n>] [--force]";

    public static GeneratorOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? outputPath = null;
        long? sizeInBytes = null;
        int? seed = null;
        int uniqueStringCount = 1000;
        int maxNumber = 1_000_000_000;
        bool force = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output":
                    EnsureNotSet(outputPath, "--output");
                    outputPath = ReadValue(args, ref i, "--output");
                    break;
                case "--size":
                    EnsureNotSet(sizeInBytes, "--size");
                    sizeInBytes = ByteSize.Parse(ReadValue(args, ref i, "--size"));
                    break;
                case "--seed":
                    EnsureNotSet(seed, "--seed");
                    seed = ParseInt(ReadValue(args, ref i, "--seed"), "--seed");
                    break;
                case "--unique-strings":
                    uniqueStringCount = ParsePositiveInt(ReadValue(args, ref i, "--unique-strings"), "--unique-strings");
                    break;
                case "--max-number":
                    maxNumber = ParsePositiveInt(ReadValue(args, ref i, "--max-number"), "--max-number");
                    break;
                case "--force":
                    force = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.{Environment.NewLine}{Usage}");
            }
        }

        if (outputPath is null)
        {
            throw new ArgumentException($"--output is required.{Environment.NewLine}{Usage}");
        }

        if (sizeInBytes is null)
        {
            throw new ArgumentException($"--size is required.{Environment.NewLine}{Usage}");
        }

        if (sizeInBytes.Value != 0 && sizeInBytes.Value < FileWriter.MinLineLength)
        {
            throw new ArgumentOutOfRangeException(
                "--size",
                sizeInBytes.Value,
                $"Size must be 0 or at least {FileWriter.MinLineLength} bytes.");
        }

        return new GeneratorOptions
        {
            OutputPath = outputPath,
            SizeInBytes = sizeInBytes.Value,
            Seed = seed ?? Random.Shared.Next(),
            UniqueStringCount = uniqueStringCount,
            MaxNumber = maxNumber,
            Force = force,
        };
    }

    private static void EnsureNotSet<T>(T? current, string name)
    {
        if (current is not null)
        {
            throw new ArgumentException($"'{name}' is specified more than once.");
        }
    }

    private static string ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"'{name}' requires a value.{Environment.NewLine}{Usage}");
        }

        string value = args[++index];
        if (value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{name}' requires a value.{Environment.NewLine}{Usage}");
        }

        return value;
    }

    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, out int parsed))
        {
            throw new FormatException($"'{name}' must be an integer. Got '{value}'.");
        }

        return parsed;
    }

    private static int ParsePositiveInt(string value, string name)
    {
        int parsed = ParseInt(value, name);
        if (parsed < 1)
        {
            throw new ArgumentOutOfRangeException(name, parsed, $"'{name}' must be at least 1.");
        }

        return parsed;
    }
}
