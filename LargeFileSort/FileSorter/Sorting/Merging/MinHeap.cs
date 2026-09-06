namespace FileSorter.Sorting.Merging;

public sealed class MinHeap
{
    private readonly int[] _items;
    private readonly Comparison<int> _compare;
    private int _count;

    public MinHeap(int capacity, Comparison<int> compare)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentNullException.ThrowIfNull(compare);

        _items = new int[capacity];
        _compare = compare;
    }

    public int Count => _count;

    public int Peek()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("Heap is empty.");
        }

        return _items[0];
    }

    public void Add(int item)
    {
        if (_count == _items.Length)
        {
            throw new InvalidOperationException("Heap is full.");
        }

        _items[_count] = item;
        SiftUp(_count);
        _count++;
    }

    /// <summary>
    /// Reorders after the current top key changed in place.
    /// The new key must not be smaller than the previous top key.
    /// </summary>
    public void ReplaceTop()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("Heap is empty.");
        }

        SiftDown(0);
    }

    public void RemoveTop()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("Heap is empty.");
        }

        _count--;
        if (_count == 0)
        {
            return;
        }

        _items[0] = _items[_count];
        SiftDown(0);
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_compare(_items[index], _items[parent]) >= 0)
            {
                break;
            }

            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = (index * 2) + 1;
            int right = left + 1;
            int smallest = index;

            if (left < _count && _compare(_items[left], _items[smallest]) < 0)
            {
                smallest = left;
            }

            if (right < _count && _compare(_items[right], _items[smallest]) < 0)
            {
                smallest = right;
            }

            if (smallest == index)
            {
                break;
            }

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int left, int right) =>
        (_items[left], _items[right]) = (_items[right], _items[left]);
}
