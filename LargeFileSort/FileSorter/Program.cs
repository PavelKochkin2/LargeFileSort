using FileSorter.Cli;
using FileSorter.Sorting;
using FileSorter.Verification;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    (SortOptions parsed, bool verify) = SorterCli.Parse(args);
    SortOptions options = new()
    {
        InputPath = parsed.InputPath,
        OutputPath = parsed.OutputPath,
        TempDirectory = parsed.TempDirectory,
        ChunkSize = parsed.ChunkSize,
        MaxLineLength = parsed.MaxLineLength,
        MaxFanIn = parsed.MaxFanIn,
        DegreeOfParallelism = parsed.DegreeOfParallelism,
        MaxMemoryBytes = parsed.MaxMemoryBytes,
        KeepTemp = parsed.KeepTemp,
        CancellationToken = cts.Token,
    };

    SortResult result = new ExternalSorter().Sort(options);
    Console.WriteLine(
        $"Sorted {options.InputPath} -> {options.OutputPath} (runs: {result.RunCount}, merge passes: {result.MergePassCount}, fast path: {result.UsedFastPath}).");

    if (verify)
    {
        SortedFileVerifier.Verify(options.InputPath, options.OutputPath);
        Console.WriteLine("Verification succeeded.");
    }

    return 0;
}
catch (Exception ex) when (IsCancellation(ex))
{
    Console.Error.WriteLine("Sort cancelled.");
    return 1;
}
catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or UnauthorizedAccessException or InvalidDataException or OverflowException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static bool IsCancellation(Exception ex) =>
    ex is OperationCanceledException
    || (ex is AggregateException aggregate
        && aggregate.Flatten().InnerExceptions.All(inner => inner is OperationCanceledException));
