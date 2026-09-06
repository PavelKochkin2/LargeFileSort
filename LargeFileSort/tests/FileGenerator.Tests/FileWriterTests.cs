using System.Text;
using System.Text.RegularExpressions;

namespace FileGenerator.Tests;

public class FileWriterTests
{
    private static readonly Regex LinePattern = new(@"^\d+\. .+$", RegexOptions.Compiled);
    private readonly FileWriter _writer = new();

    [Fact]
    public void Write_ZeroSize_CreatesEmptyFile()
    {
        string path = NewTempPath();

        try
        {
            _writer.Write(path, sizeInBytes: 0, new LineFactory(3, 10, seed: 1));

            Assert.Equal(0, new FileInfo(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Write_SizeBelowMinimum_Throws(long size)
    {
        string path = NewTempPath();

        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Write(path, size, new LineFactory(3, 10, seed: 1)));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(17)]
    [InlineData(64)]
    [InlineData(1000)]
    public void Write_ProducesExactFileSize(long size)
    {
        string path = NewTempPath();

        try
        {
            _writer.Write(path, size, new LineFactory(4, 100, seed: 42));

            Assert.Equal(size, new FileInfo(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_SameSeed_ProducesIdenticalFiles()
    {
        string firstPath = NewTempPath();
        string secondPath = NewTempPath();

        try
        {
            const long size = 512;
            _writer.Write(firstPath, size, new LineFactory(4, 100, seed: 42));
            _writer.Write(secondPath, size, new LineFactory(4, 100, seed: 42));

            Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Fact]
    public void Write_RepeatsTextFromSmallPool()
    {
        string path = NewTempPath();

        try
        {
            _writer.Write(path, sizeInBytes: 800, new LineFactory(3, 50, seed: 7));

            string[] texts = ReadBodies(path)
                .SkipLast(1)
                .Select(line => line[(line.IndexOf(' ') + 1)..])
                .ToArray();

            Assert.InRange(texts.Distinct().Count(), 1, 3);
            Assert.True(texts.Length > texts.Distinct().Count());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(256)]
    public void Write_EveryLineMatchesContract(long size)
    {
        string path = NewTempPath();

        try
        {
            _writer.Write(path, size, new LineFactory(4, 100, seed: 9));

            string[] bodies = ReadBodies(path);

            Assert.NotEmpty(bodies);
            Assert.All(bodies, body => Assert.Matches(LinePattern, body));
            Assert.All(bodies, body =>
            {
                Assert.DoesNotContain('\n', body);
                Assert.DoesNotContain('\r', body);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string[] ReadBodies(string path)
    {
        string content = File.ReadAllText(path, Encoding.UTF8);
        Assert.EndsWith("\n", content);

        return content[..^1].Split('\n');
    }

    private static string NewTempPath() => Path.Combine(Path.GetTempPath(), $"lfs-{Guid.NewGuid():N}.txt");
}
