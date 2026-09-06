using FileSorter.Sorting;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Cli;

public static class SorterCli
{
    public const string Usage =
        "Usage: FileSorter --input <path> --output <path> [--temp <dir>] [--chunk-size <size>] [--max-memory <size>] [--max-fan-in <n>] [--max-line-length <size>] [--degree-of-parallelism <n>] [--keep-temp] [--verify]";

    public static (SortOptions Options, bool Verify) Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? input = null;
        string? output = null;
        string? temp = null;
        int? chunkSize = null;
        long? maxMemory = null;
        int? maxFanIn = null;
        int? maxLineLength = null;
        int? degreeOfParallelism = null;
        bool? keepTemp = null;
        bool? verify = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input":
                    EnsureNotSet(input, "--input");
                    input = ReadValue(args, ref i, "--input");
                    break;
                case "--output":
                    EnsureNotSet(output, "--output");
                    output = ReadValue(args, ref i, "--output");
                    break;
                case "--temp":
                    EnsureNotSet(temp, "--temp");
                    temp = ReadValue(args, ref i, "--temp");
                    break;
                case "--chunk-size":
                    EnsureNotSet(chunkSize, "--chunk-size");
                    chunkSize = ParseSizeInt(ReadValue(args, ref i, "--chunk-size"), "--chunk-size");
                    break;
                case "--max-memory":
                    EnsureNotSet(maxMemory, "--max-memory");
                    maxMemory = ByteSize.Parse(ReadValue(args, ref i, "--max-memory"));
                    break;
                case "--max-fan-in":
                    EnsureNotSet(maxFanIn, "--max-fan-in");
                    maxFanIn = ParseAtLeast(ReadValue(args, ref i, "--max-fan-in"), "--max-fan-in", 2);
                    break;
                case "--max-line-length":
                    EnsureNotSet(maxLineLength, "--max-line-length");
                    maxLineLength = ParseSizeInt(ReadValue(args, ref i, "--max-line-length"), "--max-line-length");
                    break;
                case "--degree-of-parallelism":
                    EnsureNotSet(degreeOfParallelism, "--degree-of-parallelism");
                    degreeOfParallelism = ParsePositiveInt(
                        ReadValue(args, ref i, "--degree-of-parallelism"),
                        "--degree-of-parallelism");
                    break;
                case "--keep-temp":
                    EnsureNotSet(keepTemp, "--keep-temp");
                    keepTemp = true;
                    break;
                case "--verify":
                    EnsureNotSet(verify, "--verify");
                    verify = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.{Environment.NewLine}{Usage}");
            }
        }

        if (input is null || output is null)
        {
            throw new ArgumentException($"--input and --output are required.{Environment.NewLine}{Usage}");
        }

        return (new SortOptions
        {
            InputPath = input,
            OutputPath = output,
            TempDirectory = temp,
            ChunkSize = chunkSize ?? ChunkReader.DefaultBufferSize,
            MaxLineLength = maxLineLength ?? ChunkReader.DefaultMaxLineLength,
            MaxFanIn = maxFanIn ?? 64,
            DegreeOfParallelism = degreeOfParallelism ?? Math.Min(Environment.ProcessorCount, 8),
            MaxMemoryBytes = maxMemory,
            KeepTemp = keepTemp ?? false,
        }, verify ?? false);
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
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{name}' requires a value.{Environment.NewLine}{Usage}");
        }

        return args[++index];
    }

    private static int ParsePositiveInt(string value, string name)
    {
        if (!int.TryParse(value, out int parsed) || parsed < 1)
        {
            throw new ArgumentOutOfRangeException(name, value, $"'{name}' must be a positive integer.");
        }

        return parsed;
    }

    private static int ParseAtLeast(string value, string name, int minimum)
    {
        int parsed = ParsePositiveInt(value, name);
        if (parsed < minimum)
        {
            throw new ArgumentOutOfRangeException(name, parsed, $"'{name}' must be at least {minimum}.");
        }

        return parsed;
    }

    private static int ParseSizeInt(string value, string name)
    {
        long parsed = ByteSize.Parse(value);
        if (parsed < 1)
        {
            throw new ArgumentOutOfRangeException(name, value, $"'{name}' must be at least 1 byte.");
        }

        if (parsed > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, $"'{name}' must be at most 2 GiB.");
        }

        return (int)parsed;
    }
}
