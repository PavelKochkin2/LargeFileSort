using FileSorter.Sorting.Parsing;

namespace FileSorter.Sorting.Merging;

public sealed class KWayMerger
{
    public void Merge(
        IReadOnlyList<string> runPaths,
        string outputPath,
        int bufferSize = 1024 * 1024,
        int maxLineLength = ChunkReader.DefaultMaxLineLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runPaths);
        ArgumentNullException.ThrowIfNull(outputPath);

        var readers = new RunReader[runPaths.Count];

        try
        {
            var heap = new MinHeap(runPaths.Count, CompareRuns);

            for (int i = 0; i < runPaths.Count; i++)
            {
                readers[i] = new RunReader(runPaths[i], bufferSize, maxLineLength);
                if (readers[i].MoveNext())
                {
                    heap.Add(i);
                }
            }

            using FileStream output = File.Create(outputPath);

            while (heap.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int runIndex = heap.Peek();
                LineRef line = readers[runIndex].Current;
                output.Write(readers[runIndex].Buffer.AsSpan(line.Start, line.End - line.Start));
                output.WriteByte((byte)'\n');

                if (readers[runIndex].MoveNext())
                {
                    heap.ReplaceTop();
                }
                else
                {
                    heap.RemoveTop();
                }
            }
        }
        finally
        {
            foreach (RunReader? reader in readers)
            {
                reader?.Dispose();
            }
        }

        int CompareRuns(int left, int right) =>
            LineComparer.Compare(
                readers[left].Buffer,
                readers[left].Current,
                readers[right].Buffer,
                readers[right].Current);
    }
}
