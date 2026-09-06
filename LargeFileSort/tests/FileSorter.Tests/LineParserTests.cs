using System.Text;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class LineParserTests
{
    [Fact]
    public void Parse_SplitsNumberAndText()
    {
        byte[] line = Utf8("415. Apple");
        LineRef parsed = LineParser.Parse(line);

        Assert.Equal("415", Encoding.UTF8.GetString(parsed.Number(line)));
        Assert.Equal("Apple", Encoding.UTF8.GetString(parsed.Text(line)));
    }

    [Fact]
    public void Parse_KeepsPeriodInsideText()
    {
        byte[] line = Utf8("32. Cherry is the best. Really");
        LineRef parsed = LineParser.Parse(line);

        Assert.Equal("32", Encoding.UTF8.GetString(parsed.Number(line)));
        Assert.Equal("Cherry is the best. Really", Encoding.UTF8.GetString(parsed.Text(line)));
    }

    [Fact]
    public void Parse_KeepsLeadingZerosInNumber()
    {
        byte[] line = Utf8("007. Apple");
        LineRef parsed = LineParser.Parse(line);

        Assert.Equal("007", Encoding.UTF8.GetString(parsed.Number(line)));
        Assert.Equal("Apple", Encoding.UTF8.GetString(parsed.Text(line)));
    }

    [Fact]
    public void Parse_AllowsEmptyText()
    {
        byte[] line = Utf8("1. ");
        LineRef parsed = LineParser.Parse(line);

        Assert.Equal("1", Encoding.UTF8.GetString(parsed.Number(line)));
        Assert.Equal(0, parsed.Text(line).Length);
    }

    [Fact]
    public void Parse_KeepsNonAsciiText()
    {
        byte[] line = Utf8("1. Яблоко");
        LineRef parsed = LineParser.Parse(line);

        Assert.Equal("1", Encoding.UTF8.GetString(parsed.Number(line)));
        Assert.Equal("Яблоко", Encoding.UTF8.GetString(parsed.Text(line)));
    }

    [Fact]
    public void Parse_BomPrefixedLine_ThrowsFormatException()
    {
        byte[] line = [(byte)0xEF, (byte)0xBB, (byte)0xBF, ..Utf8("1. Apple")];

        Assert.Throws<FormatException>(() => LineParser.Parse(line));
    }

    [Fact]
    public void Parse_UsesOffsetsInsideLargerBuffer()
    {
        byte[] buffer = Utf8("xx1. Apple\nyy");
        LineRef parsed = LineParser.Parse(buffer, start: 2, end: 10);

        Assert.Equal("1", Encoding.UTF8.GetString(parsed.Number(buffer)));
        Assert.Equal("Apple", Encoding.UTF8.GetString(parsed.Text(buffer)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Apple")]
    [InlineData(" . Apple")]
    [InlineData("1 Apple")]
    [InlineData("1.")]
    [InlineData("1.Apple")]
    [InlineData(" 1. Apple")]
    [InlineData("1. Apple\n")]
    [InlineData("1. Apple\r")]
    public void Parse_InvalidLine_ThrowsFormatException(string line)
    {
        Assert.Throws<FormatException>(() => LineParser.Parse(Utf8(line)));
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
