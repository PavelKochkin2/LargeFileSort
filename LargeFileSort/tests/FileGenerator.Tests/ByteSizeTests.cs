using FileGenerator.Cli;

namespace FileGenerator.Tests;

public class ByteSizeTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1024", 1024)]
    [InlineData("1KB", 1024)]
    [InlineData("1kb", 1024)]
    [InlineData("1K", 1024)]
    [InlineData("500MB", 500L * 1024 * 1024)]
    [InlineData("10MB", 10L * 1024 * 1024)]
    [InlineData("1GB", 1024L * 1024 * 1024)]
    [InlineData("1gb", 1024L * 1024 * 1024)]
    [InlineData("1G", 1024L * 1024 * 1024)]
    [InlineData("2TB", 2L * 1024 * 1024 * 1024 * 1024)]
    [InlineData(" 10MB ", 10L * 1024 * 1024)]
    [InlineData("1 GB", 1024L * 1024 * 1024)]
    public void Parse_ValidSize_ReturnsBytes(string input, long expected)
    {
        Assert.Equal(expected, ByteSize.Parse(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_MissingSize_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => ByteSize.Parse(input));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5GB")]
    [InlineData("-1")]
    [InlineData("GB")]
    [InlineData("1XB")]
    public void Parse_InvalidSize_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => ByteSize.Parse(input));
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ByteSize.Parse(null!));
    }

    [Fact]
    public void Parse_Overflow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSize.Parse("10000000TB"));
    }
}
