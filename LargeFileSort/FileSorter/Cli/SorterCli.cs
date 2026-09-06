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
        int maxFanIn = 64;
        int maxLineLength = ChunkReader.DefaultMaxLineLength;
        int degreeOfParallelism = Math.Min(Environment.ProcessorCount, 8);
        bool keepTemp = false;
        bool verify = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input":
                    input = ReadValue(args, ref i, "--input");
                    break;
                case "--output":
                    output = ReadValue(args, ref i, "--output");
                    break;
                case "--temp":
                    temp = ReadValue(args, ref i, "--temp");
                    break;
                case "--chunk-size":
                    chunkSize = checked((int)ByteSize.Parse(ReadValue(args, ref i, "--chunk-size")));
                    break;
                case "--max-memory":
                    maxMemory = ByteSize.Parse(ReadValue(args, ref i, "--max-memory"));
                    break;
                case "--max-fan-in":
                    maxFanIn = ParsePositiveInt(ReadValue(args, ref i, "--max-fan-in"), "--max-fan-in");
                    break;
                case "--max-line-length":
                    maxLineLength = checked((int)ByteSize.Parse(ReadValue(args, ref i, "--max-line-length")));
                    break;
                case "--degree-of-parallelism":
                    degreeOfParallelism = ParsePositiveInt(
                        ReadValue(args, ref i, "--degree-of-parallelism"),
                        "--degree-of-parallelism");
                    break;
                case "--keep-temp":
                    keepTemp = true;
                    break;
                case "--verify":
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
            MaxLineLength = maxLineLength,
            MaxFanIn = maxFanIn,
            DegreeOfParallelism = degreeOfParallelism,
            MaxMemoryBytes = maxMemory,
            KeepTemp = keepTemp,
        }, verify);
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
}
