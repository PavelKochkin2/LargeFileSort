using FileSorter.Cli;

namespace FileSorter.Tests;

public class ByteSizeTests
{
    [Theory]
    [InlineData("1024", 1024)]
    [InlineData("1KB", 1024)]
    [InlineData("10MB", 10L * 1024 * 1024)]
    [InlineData("1GB", 1024L * 1024 * 1024)]
    [InlineData("2TB", 2L * 1024 * 1024 * 1024 * 1024)]
    public void Parse_ValidSize_ReturnsBytes(string input, long expected)
    {
        Assert.Equal(expected, ByteSize.Parse(input));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1XB")]
    public void Parse_InvalidSize_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => ByteSize.Parse(input));
    }

    [Fact]
    public void Parse_Overflow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSize.Parse("10000000TB"));
    }
}
