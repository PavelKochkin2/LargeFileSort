using FileSorter.Sorting.Parsing;

namespace FileSorter.Sorting;

public sealed class SortOptions
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public string? TempDirectory { get; init; }
    public int ChunkSize { get; init; } = ChunkReader.DefaultBufferSize;
    public int MaxLineLength { get; init; } = ChunkReader.DefaultMaxLineLength;
    public int MaxFanIn { get; init; } = 64;
    public int DegreeOfParallelism { get; init; } = 1;
    public long? MaxMemoryBytes { get; init; }
    public bool KeepTemp { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
