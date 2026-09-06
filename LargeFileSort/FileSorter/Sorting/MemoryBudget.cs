namespace FileSorter.Sorting;

public static class MemoryBudget
{
    public const int MinChunkSize = 64 * 1024 * 1024;
    public const int MaxChunkSize = 1024 * 1024 * 1024;

    public static int ResolveChunkSize(long maxMemoryBytes, int workers, int queueDepth = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMemoryBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(queueDepth, 1);

        int slots = workers + queueDepth;
        long raw = (long)(maxMemoryBytes / (slots * 1.6));
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
