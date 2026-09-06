namespace FileGenerator.Tests;

public class GeneratedLineTests
{
    [Fact]
    public void ToFileText_FormatsNumberDotSpaceText()
    {
        var line = new GeneratedLine(415, "Apple");

        Assert.Equal("415. Apple", line.ToFileText());
    }

    [Fact]
    public void ToFileText_KeepsPeriodInsideText()
    {
        var line = new GeneratedLine(32, "Cherry is the best. Really");

        Assert.Equal("32. Cherry is the best. Really", line.ToFileText());
    }

    [Fact]
    public void ToFileText_DoesNotAddNewline()
    {
        string text = new GeneratedLine(1, "Apple").ToFileText();

        Assert.DoesNotContain('\n', text);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void ToFileText_DoesNotPadNumberWithLeadingZeros()
    {
        Assert.Equal("7. Apple", new GeneratedLine(7, "Apple").ToFileText());
    }
}
