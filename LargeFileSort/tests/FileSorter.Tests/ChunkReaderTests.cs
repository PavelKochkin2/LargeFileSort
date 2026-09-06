using System.Text;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class ChunkReaderTests
{
    [Fact]
    public void ReadsLfLines()
    {
        string[] lines = ReadAll(Utf8("1. Apple\n415. Apple\n"), bufferSize: 64);

        Assert.Equal(["1. Apple", "415. Apple"], lines);
    }

    [Fact]
    public void ReadsCrlfLines()
    {
        string[] lines = ReadAll(Utf8("1. Apple\r\n2. Banana\r\n"), bufferSize: 64);

        Assert.Equal(["1. Apple", "2. Banana"], lines);
    }

    [Fact]
    public void ReadsMixedNewlines()
    {
        string[] lines = ReadAll(Utf8("1. Apple\r\n2. Banana\n3. Cherry\r\n"), bufferSize: 8);

        Assert.Equal(["1. Apple", "2. Banana", "3. Cherry"], lines);
    }

    [Fact]
    public void ReadsLastLineWithoutTrailingNewline()
    {
        string[] lines = ReadAll(Utf8("1. Apple\n2. Banana"), bufferSize: 64);

        Assert.Equal(["1. Apple", "2. Banana"], lines);
    }

    [Fact]
    public void FirstChunk_IncludesLastLineWithoutNewline_WhenFileFitsInBuffer()
    {
        byte[] data = Utf8("1. Apple\n2. Banana");
        using var reader = new ChunkReader(new MemoryStream(data), bufferSize: data.Length, maxLineLength: 1024);

        Assert.True(reader.MoveNextChunk());
        Assert.Equal(2, reader.Lines.Length);
        Assert.False(reader.MoveNextChunk());
    }

    [Fact]
    public void EmptyFile_ReturnsNoChunks()
    {
        Assert.Empty(ReadAll([], bufferSize: 8));
    }

    [Fact]
    public void SkipsLeadingUtf8Bom()
    {
        byte[] data = [0xEF, 0xBB, 0xBF, ..Utf8("1. Apple\n2. Banana\n")];

        Assert.Equal(["1. Apple", "2. Banana"], ReadAll(data, bufferSize: 4));
    }

    [Fact]
    public void TinyBuffer_StillReturnsEveryLine()
    {
        string[] lines = ReadAll(
            Utf8("1. Apple\n2. Banana is yellow\n32. Cherry is the best\n"),
            bufferSize: 1);

        Assert.Equal(
            ["1. Apple", "2. Banana is yellow", "32. Cherry is the best"],
            lines);
    }

    [Fact]
    public void MultibyteUtf8Character_SplitAcrossBufferReads()
    {
        string[] lines = ReadAll(Utf8("1. Яблоко\n2. Apple\n"), bufferSize: 1);

        Assert.Equal(["1. Яблоко", "2. Apple"], lines);
    }

    [Fact]
    public void GrowsBufferForLineLongerThanInitialCapacity()
    {
        string[] lines = ReadAll(Utf8("1. Something something something\n"), bufferSize: 4, maxLineLength: 64);

        Assert.Equal(["1. Something something something"], lines);
    }

    [Fact]
    public void LineLongerThanMaxLength_Throws()
    {
        byte[] data = Utf8("1. This line is too long\n");

        FormatException ex = Assert.Throws<FormatException>(
            () => ReadAll(data, bufferSize: 4, maxLineLength: 8));

        Assert.Contains("max length", ex.Message, StringComparison.Ordinal);
        Assert.Contains("offset", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LineLongerThanMaxLength_ThrowsEvenWhenBufferIsLarger()
    {
        byte[] data = Utf8("1. This line is too long\n");

        FormatException ex = Assert.Throws<FormatException>(
            () => ReadAll(data, bufferSize: 1024, maxLineLength: 8));

        Assert.Contains("max length", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Buffer_StartsAtRequestedSize_EvenWhenLargerThanMaxLineLength()
    {
        using var reader = new ChunkReader(new MemoryStream(Utf8("1. A\n")), bufferSize: 2048, maxLineLength: 32);

        Assert.Equal(2048, reader.Buffer.Length);
        Assert.True(reader.MoveNextChunk());
        int payload = 0;
        foreach (LineRef line in reader.Lines)
        {
            payload += line.End - line.Start + 1;
        }

        Assert.True(payload <= 2048);
    }

    [Fact]
    public void InvalidLine_IncludesLineNumberAndByteOffset()
    {
        FormatException ex = Assert.Throws<FormatException>(
            () => ReadAll(Utf8("1. Apple\nnot-a-line\n"), bufferSize: 64));

        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("offset 9", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidLine_ReportsFileOffsetAfterBomAndLaterBuffer()
    {
        byte[] data = [0xEF, 0xBB, 0xBF, ..Utf8("1. Apple\n2. Banana\nnot-a-line\n")];

        FormatException ex = Assert.Throws<FormatException>(() => ReadAll(data, bufferSize: 16));

        Assert.Contains("line 3", ex.Message, StringComparison.Ordinal);
        Assert.Contains("offset 22", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BomOnlyFile_ReturnsNoChunks()
    {
        Assert.Empty(ReadAll([0xEF, 0xBB, 0xBF], bufferSize: 1));
    }

    private static string[] ReadAll(byte[] data, int bufferSize, int maxLineLength = 1024)
    {
        using var reader = new ChunkReader(new MemoryStream(data), bufferSize, maxLineLength);
        var lines = new List<string>();

        while (reader.MoveNextChunk())
        {
            foreach (LineRef line in reader.Lines)
            {
                string number = Encoding.UTF8.GetString(line.Number(reader.Buffer));
                string text = Encoding.UTF8.GetString(line.Text(reader.Buffer));
                lines.Add($"{number}. {text}");
            }
        }

        return [.. lines];
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
