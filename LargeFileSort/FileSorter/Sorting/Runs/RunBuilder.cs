using FileSorter.Sorting.Parsing;

namespace FileSorter.Sorting.Runs;

public sealed class RunBuilder
{
    public void Write(string path, byte[] buffer, ReadOnlySpan<LineRef> lines)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(buffer);

        LineRef[] sorted = lines.ToArray();
        sorted.AsSpan().Sort(new LineComparer(buffer));

        using FileStream stream = File.Create(path);
        foreach (LineRef line in sorted)
        {
            stream.Write(buffer.AsSpan(line.Start, line.End - line.Start));
            stream.WriteByte((byte)'\n');
        }
    }
}
