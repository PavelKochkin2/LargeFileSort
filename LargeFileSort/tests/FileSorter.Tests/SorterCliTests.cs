using FileSorter.Cli;

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
    }

    [Fact]
    public void Parse_MissingInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => SorterCli.Parse(["--output", "out.txt"]));
    }
}
