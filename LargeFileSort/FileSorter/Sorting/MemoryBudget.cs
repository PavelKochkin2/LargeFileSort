namespace FileSorter.Sorting;

public static class MemoryBudget
{
    public const int MinChunkSize = 64 * 1024 * 1024;
    // LineRef offsets are int, so a single buffer cannot exceed 2 GiB; 1 GiB leaves room for LineRef[].
    public const int MaxChunkSize = 1024 * 1024 * 1024;
    public const int DefaultQueueDepth = 1;
    // LineRef array + sort scratch on top of the raw byte buffer.
    private const double OverheadFactor = 1.6;

    public static int ResolveChunkSize(long maxMemoryBytes, int workers, int queueDepth = DefaultQueueDepth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMemoryBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(queueDepth, 1);

        int slots = workers + queueDepth;
        long raw = (long)(maxMemoryBytes / (slots * OverheadFactor));
        if (raw < MinChunkSize)
        {
            return MinChunkSize;
        }

        if (raw > MaxChunkSize)
        {
            return MaxChunkSize;
        }

        return (int)raw;
    }
}
