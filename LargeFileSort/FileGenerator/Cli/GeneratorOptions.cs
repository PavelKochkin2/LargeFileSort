namespace FileGenerator.Cli;

public sealed class GeneratorOptions
{
    public required string OutputPath { get; init; }
    public required long SizeInBytes { get; init; }
    public int Seed { get; init; }
    public int UniqueStringCount { get; init; } = 1000;
    public int MaxNumber { get; init; } = 1_000_000_000;
    public bool Force { get; init; }
}
