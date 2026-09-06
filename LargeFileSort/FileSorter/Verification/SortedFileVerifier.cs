using FileSorter.Sorting.Parsing;

namespace FileSorter.Verification;

public static class SortedFileVerifier
{
    public static void Verify(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        (ulong inputHash, long inputCount) = HashLines(inputPath, checkSorted: false);
        (ulong outputHash, long outputCount) = HashLines(outputPath, checkSorted: true);

        if (inputCount != outputCount || inputHash != outputHash)
        {
            throw new InvalidDataException("Output is not a permutation of the input.");
        }
    }

    private static (ulong Sum, long Count) HashLines(string path, bool checkSorted)
    {
        using var reader = new ChunkReader(File.OpenRead(path), ChunkReader.DefaultBufferSize, ChunkReader.DefaultMaxLineLength);
        ulong sum = 0;
        long count = 0;
        byte[]? previous = null;

        while (reader.MoveNextChunk())
        {
            foreach (LineRef line in reader.Lines)
            {
                ReadOnlySpan<byte> current = reader.Buffer.AsSpan(line.Start, line.End - line.Start);
                if (checkSorted && previous is not null && LineComparer.CompareLines(previous, current) > 0)
                {
                    throw new InvalidDataException("Output is not sorted.");
                }

                sum += Fnv1A64(current);
                count++;
                previous = current.ToArray();
            }
        }

        return (sum, count);
    }

    private static ulong Fnv1A64(ReadOnlySpan<byte> data)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        ulong hash = offset;
        foreach (byte value in data)
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }
}
