using FileSorter.Sorting.Merging;

namespace FileSorter.Tests;

public class MinHeapTests
{
    [Fact]
    public void Peek_ReturnsSmallestItem()
    {
        var heap = new MinHeap(4, (left, right) => left.CompareTo(right));
        heap.Add(4);
        heap.Add(1);
        heap.Add(3);

        Assert.Equal(1, heap.Peek());
    }

    [Fact]
    public void RemoveTop_ExposesNextSmallest()
    {
        var heap = new MinHeap(4, (left, right) => left.CompareTo(right));
        heap.Add(4);
        heap.Add(1);
        heap.Add(3);

        heap.RemoveTop();

        Assert.Equal(3, heap.Peek());
        Assert.Equal(2, heap.Count);
    }

    [Fact]
    public void ReplaceTop_ReordersWhenKeyChanges()
    {
        int[] keys = [10, 20, 5];
        var heap = new MinHeap(3, (left, right) => keys[left].CompareTo(keys[right]));
        heap.Add(0);
        heap.Add(1);
        heap.Add(2);

        Assert.Equal(2, heap.Peek());

        keys[2] = 30;
        heap.ReplaceTop();

        Assert.Equal(0, heap.Peek());
    }
}
