using System.Text;
using FileSorter.Sorting.Parsing;
using FileSorter.Sorting.Runs;

namespace FileSorter.Tests;

public class RunBuilderTests
{
    private readonly RunBuilder _builder = new();

    [Fact]
    public void Write_EmptyLines_CreatesEmptyFile()
    {
        string path = NewTempPath();

        try
        {
            _builder.Write(path, [], []);

            Assert.Equal(0, new FileInfo(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_SortsChunkAndKeepsTheSameLines()
    {
        byte[] input = Utf8(
            "415. Apple\n" +
            "30432. Something something something\n" +
            "1. Apple\n" +
            "32. Cherry is the best\n" +
            "2. Banana is yellow\n");

        string[] expected =
        [
            "1. Apple",
            "415. Apple",
            "2. Banana is yellow",
            "32. Cherry is the best",
            "30432. Something something something",
        ];

        string path = NewTempPath();

        try
        {
            WriteSingleChunk(path, input, bufferSize: 1024);

            Assert.Equal(expected, ReadRun(path));
            Assert.Equal(expected, Oracle(input));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_PreservesDuplicateLines()
    {
        byte[] input = Utf8("2. Apple\n1. Apple\n1. Apple\n");
        string path = NewTempPath();

        try
        {
            WriteSingleChunk(path, input, bufferSize: 1024);

            Assert.Equal(["1. Apple", "1. Apple", "2. Apple"], ReadRun(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_UsesLfEvenWhenInputWasCrlf()
    {
        byte[] input = Utf8("2. Banana\r\n1. Apple\r\n");
        string path = NewTempPath();

        try
        {
            WriteSingleChunk(path, input, bufferSize: 1024);

            byte[] run = File.ReadAllBytes(path);
            Assert.Equal(Utf8("1. Apple\n2. Banana\n"), run);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_EachChunkRunIsSortedIndependently()
    {
        byte[] input = Utf8("9. Zebra\n1. Apple\n3. Mango\n2. Banana\n");
        string firstRun = NewTempPath();
        string secondRun = NewTempPath();

        try
        {
            using var reader = new ChunkReader(new MemoryStream(input), bufferSize: 12, maxLineLength: 1024);
            Assert.True(reader.MoveNextChunk());
            _builder.Write(firstRun, reader.Buffer, reader.Lines);
            string[] first = ReadRun(firstRun);
            Assert.Equal(OracleSort(first), first);

            Assert.True(reader.MoveNextChunk());
            _builder.Write(secondRun, reader.Buffer, reader.Lines);
            string[] second = ReadRun(secondRun);
            Assert.Equal(OracleSort(second), second);
        }
        finally
        {
            File.Delete(firstRun);
            File.Delete(secondRun);
        }
    }

    private void WriteSingleChunk(string path, byte[] input, int bufferSize)
    {
        using var reader = new ChunkReader(new MemoryStream(input), bufferSize, maxLineLength: 1024);
        Assert.True(reader.MoveNextChunk());
        _builder.Write(path, reader.Buffer, reader.Lines);
        Assert.False(reader.MoveNextChunk());
    }

    private static string[] ReadRun(string path)
    {
        using var reader = new ChunkReader(File.OpenRead(path), bufferSize: 1024, maxLineLength: 1024);
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

    private static string[] Oracle(byte[] input)
    {
        string text = Encoding.UTF8.GetString(input).Replace("\r\n", "\n").TrimEnd('\n');
        return OracleSort(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string[] OracleSort(IEnumerable<string> lines) =>
        lines.Order(Comparer<string>.Create(CompareLines)).ToArray();

    private static int CompareLines(string left, string right) =>
        LineComparer.CompareLines(Utf8(left), Utf8(right));

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static string NewTempPath() => Path.Combine(Path.GetTempPath(), $"lfs-run-{Guid.NewGuid():N}.txt");
}
