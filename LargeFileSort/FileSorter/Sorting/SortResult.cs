namespace FileSorter.Sorting;

public sealed class SortResult
{
    public int RunCount { get; init; }
    public int MergePassCount { get; init; }
    public bool UsedFastPath { get; init; }
}
