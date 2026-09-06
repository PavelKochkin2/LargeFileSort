using System.Text;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class LineComparerTests
{
    [Fact]
    public void Compare_OrdersByTextFirst()
    {
        Assert.True(Compare("1. Apple", "2. Banana") < 0);
        Assert.True(Compare("2. Banana", "1. Apple") > 0);
    }

    [Fact]
    public void Compare_UsesOrdinalTextOrder()
    {
        Assert.True(Compare("1. Apple", "1. apple") < 0);
    }

    [Fact]
    public void Compare_SameText_OrdersByNumericValue()
    {
        Assert.True(Compare("1. Apple", "415. Apple") < 0);
        Assert.True(Compare("9. Apple", "10. Apple") < 0);
        Assert.True(Compare("10. Apple", "9. Apple") > 0);
    }

    [Fact]
    public void Compare_NumericEquality_UsesOriginalNumberBytes()
    {
        Assert.True(Compare("007. Apple", "7. Apple") < 0);
        Assert.True(Compare("7. Apple", "007. Apple") > 0);
        Assert.Equal(0, Compare("00. Apple", "00. Apple"));
    }

    [Fact]
    public void Compare_LeadingZeros_DoNotIncreaseNumericMagnitude()
    {
        Assert.True(Compare("010. Apple", "10. Apple") < 0);
        Assert.True(Compare("10. Apple", "010. Apple") > 0);
        Assert.True(Compare("001. Apple", "2. Apple") < 0);
        Assert.True(Compare("2. Apple", "001. Apple") > 0);
    }

    [Fact]
    public void Compare_NumbersWiderThanInt64_UsesDigitMagnitude()
    {
        Assert.True(Compare("9223372036854775808. Apple", "9223372036854775807. Apple") > 0);
        Assert.True(Compare("9223372036854775807. Apple", "9223372036854775808. Apple") < 0);
    }

    [Fact]
    public void Compare_NonAsciiText_UsesUtf8OrdinalOrder()
    {
        Assert.True(Compare("1. Apple", "1. Яблоко") < 0);
        Assert.True(Compare("1. Яблоко", "1. Apple") > 0);
    }

    [Fact]
    public void Compare_IdenticalLines_AreEqual()
    {
        Assert.Equal(0, Compare("415. Apple", "415. Apple"));
    }

    [Fact]
    public void Compare_IsAntiSymmetricForSampleSet()
    {
        string[] lines =
        [
            "1. Apple",
            "415. Apple",
            "007. Apple",
            "7. Apple",
            "2. Banana is yellow",
            "32. Cherry is the best",
            "30432. Something something something",
            "9. apple",
        ];

        for (int i = 0; i < lines.Length; i++)
        {
            for (int j = 0; j < lines.Length; j++)
            {
                int forward = Compare(lines[i], lines[j]);
                int backward = Compare(lines[j], lines[i]);
                Assert.Equal(Math.Sign(forward), -Math.Sign(backward));
            }
        }
    }

    [Fact]
    public void Compare_SharedBuffer_MatchesCompareLines()
    {
        byte[] buffer = Utf8("1. Apple\n415. Apple");
        LineRef first = LineParser.Parse(buffer, 0, 8);
        LineRef second = LineParser.Parse(buffer, 9, 19);
        var comparer = new LineComparer(buffer);

        Assert.Equal(
            LineComparer.CompareLines(Utf8("1. Apple"), Utf8("415. Apple")),
            comparer.Compare(first, second));
    }

    private static int Compare(string left, string right) =>
        LineComparer.CompareLines(Utf8(left), Utf8(right));

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
