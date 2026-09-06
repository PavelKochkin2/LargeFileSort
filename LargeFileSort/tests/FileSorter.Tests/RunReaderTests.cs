using System.Text;
using FileSorter.Sorting.Merging;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class RunReaderTests
{
    [Fact]
    public void MoveNext_ReadsAcrossChunks()
    {
        byte[] data = Utf8("1. Apple\n2. Banana\n3. Cherry\n");
        using var reader = new RunReader(new MemoryStream(data), bufferSize: 8, maxLineLength: 64);

        Assert.Equal(["1. Apple", "2. Banana", "3. Cherry"], ReadAll(reader));
    }

    [Fact]
    public void Current_BeforeMoveNext_Throws()
    {
        using var reader = new RunReader(new MemoryStream(Utf8("1. Apple\n")), bufferSize: 64, maxLineLength: 64);

        Assert.Throws<InvalidOperationException>(() => _ = reader.Current);
    }

    [Fact]
    public void MoveNext_EmptyStream_ReturnsFalse()
    {
        using var reader = new RunReader(new MemoryStream(), bufferSize: 16, maxLineLength: 16);

        Assert.False(reader.MoveNext());
    }

    private static string[] ReadAll(RunReader reader)
    {
        var lines = new List<string>();
        while (reader.MoveNext())
        {
            LineRef line = reader.Current;
            string number = Encoding.UTF8.GetString(line.Number(reader.Buffer));
            string text = Encoding.UTF8.GetString(line.Text(reader.Buffer));
            lines.Add($"{number}. {text}");
        }

        return [.. lines];
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
