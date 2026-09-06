using FileGenerator.Cli;

namespace FileGenerator.Tests;

public class GeneratorCliTests
{
    [Fact]
    public void Parse_RequiredArguments_MapsOutputAndSize()
    {
        GeneratorOptions options = GeneratorCli.Parse(["--output", "out.txt", "--size", "1GB"]);

        Assert.Equal("out.txt", options.OutputPath);
        Assert.Equal(1024L * 1024 * 1024, options.SizeInBytes);
        Assert.Equal(1000, options.UniqueStringCount);
        Assert.Equal(1_000_000_000, options.MaxNumber);
        Assert.False(options.Force);
    }

    [Fact]
    public void Parse_OptionalArguments_OverridesDefaults()
    {
        GeneratorOptions options = GeneratorCli.Parse(
        [
            "--output", "data.txt",
            "--size", "512",
            "--seed", "42",
            "--unique-strings", "8",
            "--max-number", "100",
            "--force",
        ]);

        Assert.Equal("data.txt", options.OutputPath);
        Assert.Equal(512, options.SizeInBytes);
        Assert.Equal(42, options.Seed);
        Assert.Equal(8, options.UniqueStringCount);
        Assert.Equal(100, options.MaxNumber);
        Assert.True(options.Force);
    }

    [Fact]
    public void Parse_MissingOutput_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeneratorCli.Parse(["--size", "1024"]));
    }

    [Fact]
    public void Parse_MissingSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeneratorCli.Parse(["--output", "out.txt"]));
    }

    [Fact]
    public void Parse_UnknownArgument_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => GeneratorCli.Parse(["--output", "out.txt", "--size", "1024", "--oops"]));
    }

    [Fact]
    public void Parse_MissingValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeneratorCli.Parse(["--output"]));
    }

    [Fact]
    public void Parse_DuplicateOutput_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => GeneratorCli.Parse(["--output", "a.txt", "--output", "b.txt", "--size", "1024"]));
    }

    [Fact]
    public void Parse_SizeBelowMinimum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GeneratorCli.Parse(["--output", "out.txt", "--size", "3"]));
    }

    [Fact]
    public void Parse_ZeroSize_IsAllowed()
    {
        GeneratorOptions options = GeneratorCli.Parse(["--output", "out.txt", "--size", "0"]);

        Assert.Equal(0, options.SizeInBytes);
    }

    [Fact]
    public void Parse_BadSeed_Throws()
    {
        Assert.Throws<FormatException>(
            () => GeneratorCli.Parse(["--output", "out.txt", "--size", "1024", "--seed", "x"]));
    }

    [Theory]
    [InlineData("--unique-strings", "0")]
    [InlineData("--max-number", "-1")]
    public void Parse_NonPositiveOptionalInt_Throws(string name, string value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GeneratorCli.Parse(["--output", "out.txt", "--size", "1024", name, value]));
    }
}
