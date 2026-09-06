using FileSorter.Sorting;

namespace FileSorter.Tests;

public class MemoryBudgetTests
{
    [Fact]
    public void ResolveChunkSize_ClampsToMinimum()
    {
        Assert.Equal(MemoryBudget.MinChunkSize, MemoryBudget.ResolveChunkSize(1024, workers: 8));
    }

    [Fact]
    public void ResolveChunkSize_UsesWorkerSlots()
    {
        int oneWorker = MemoryBudget.ResolveChunkSize(256L * 1024 * 1024, workers: 1);
        int eightWorkers = MemoryBudget.ResolveChunkSize(256L * 1024 * 1024, workers: 8);

        Assert.True(oneWorker > eightWorkers);
        Assert.InRange(oneWorker, MemoryBudget.MinChunkSize, MemoryBudget.MaxChunkSize);
    }
}
