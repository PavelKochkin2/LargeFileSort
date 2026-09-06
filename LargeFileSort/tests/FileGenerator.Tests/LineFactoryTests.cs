namespace FileGenerator.Tests;

public class LineFactoryTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var first = new LineFactory(uniqueStringCount: 4, maxNumber: 100, seed: 42);
        var second = new LineFactory(uniqueStringCount: 4, maxNumber: 100, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(first.Next(), second.Next());
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var first = new LineFactory(uniqueStringCount: 4, maxNumber: 100, seed: 1);
        var second = new LineFactory(uniqueStringCount: 4, maxNumber: 100, seed: 2);

        var firstBatch = Take(first, 10);
        var secondBatch = Take(second, 10);

        Assert.NotEqual(firstBatch, secondBatch);
    }

    [Fact]
    public void LimitedPhrasePool_RepeatsTextAndDoesNotExceedPool()
    {
        var factory = new LineFactory(uniqueStringCount: 3, maxNumber: 50, seed: 7);

        var texts = Take(factory, 20).Select(line => line.Text).ToArray();

        Assert.InRange(texts.Distinct().Count(), 1, 3);
        Assert.True(texts.Length > texts.Distinct().Count());
    }

    [Fact]
    public void SinglePhrasePool_AlwaysReturnsSameText()
    {
        var factory = new LineFactory(uniqueStringCount: 1, maxNumber: 50, seed: 3);

        string[] texts = Take(factory, 10).Select(line => line.Text).ToArray();

        Assert.All(texts, text => Assert.Equal(texts[0], text));
    }

    [Fact]
    public void Next_ReturnsNumberWithinRange()
    {
        const int maxNumber = 5;
        var factory = new LineFactory(uniqueStringCount: 4, maxNumber, seed: 11);

        Assert.All(Take(factory, 50), line => Assert.InRange(line.Number, 1, maxNumber));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(3, 0)]
    [InlineData(3, -1)]
    public void Constructor_RejectsNonPositiveArguments(int uniqueStringCount, int maxNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LineFactory(uniqueStringCount, maxNumber, seed: 1));
    }

    private static GeneratedLine[] Take(LineFactory factory, int count)
    {
        var lines = new GeneratedLine[count];
        for (int i = 0; i < count; i++)
        {
            lines[i] = factory.Next();
        }

        return lines;
    }
}
