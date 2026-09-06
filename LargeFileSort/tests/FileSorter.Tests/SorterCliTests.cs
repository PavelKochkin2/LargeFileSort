using FileSorter.Cli;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class SorterCliTests
{
    [Fact]
    public void Parse_RequiredArguments_MapsPaths()
    {
        (Sorting.SortOptions options, bool verify) = SorterCli.Parse(
            ["--input", "in.txt", "--output", "out.txt", "--verify"]);

        Assert.Equal("in.txt", options.InputPath);
        Assert.Equal("out.txt", options.OutputPath);
        Assert.True(verify);
        Assert.Equal(ChunkReader.DefaultBufferSize, options.ChunkSize);
        Assert.Equal(64, options.MaxFanIn);
        Assert.False(options.KeepTemp);
    }

    [Fact]
    public void Parse_OptionalFlags_OverrideDefaults()
    {
        (Sorting.SortOptions options, bool verify) = SorterCli.Parse(
        [
            "--input", "in.txt",
            "--output", "out.txt",
            "--temp", "tmp",
            "--chunk-size", "4MB",
            "--max-memory", "2GB",
            "--max-fan-in", "8",
            "--max-line-length", "64KB",
            "--degree-of-parallelism", "3",
            "--keep-temp",
            "--verify",
        ]);

        Assert.Equal("tmp", options.TempDirectory);
        Assert.Equal(4 * 1024 * 1024, options.ChunkSize);
        Assert.Equal(2L * 1024 * 1024 * 1024, options.MaxMemoryBytes);
        Assert.Equal(8, options.MaxFanIn);
        Assert.Equal(64 * 1024, options.MaxLineLength);
        Assert.Equal(3, options.DegreeOfParallelism);
        Assert.True(options.KeepTemp);
        Assert.True(verify);
    }

    [Fact]
    public void Parse_MissingInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => SorterCli.Parse(["--output", "out.txt"]));
    }

    [Fact]
    public void Parse_UnknownArgument_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => SorterCli.Parse(["--input", "in.txt", "--output", "out.txt", "--oops"]));
    }

    [Fact]
    public void Parse_MissingValue_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => SorterCli.Parse(["--input", "in.txt", "--output", "out.txt", "--chunk-size"]));
    }

    [Theory]
    [InlineData("--input")]
    [InlineData("--output")]
    [InlineData("--chunk-size")]
    [InlineData("--temp")]
    public void Parse_DuplicateValuedFlag_Throws(string flag)
    {
        string[] args = flag switch
        {
            "--input" => ["--input", "a.txt", "--input", "b.txt", "--output", "out.txt"],
            "--output" => ["--input", "in.txt", "--output", "a.txt", "--output", "b.txt"],
            "--chunk-size" => ["--input", "in.txt", "--output", "out.txt", "--chunk-size", "1MB", "--chunk-size", "2MB"],
            _ => ["--input", "in.txt", "--output", "out.txt", "--temp", "a", "--temp", "b"],
        };

        Assert.Throws<ArgumentException>(() => SorterCli.Parse(args));
    }

    [Fact]
    public void Parse_MaxFanInOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SorterCli.Parse(["--input", "in.txt", "--output", "out.txt", "--max-fan-in", "1"]));
    }

    [Fact]
    public void Parse_ChunkSizeOverIntMax_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SorterCli.Parse(["--input", "in.txt", "--output", "out.txt", "--chunk-size", "3GB"]));
    }
}
