namespace FileGenerator;

public sealed class LineFactory
{
    private static readonly string[] BasePhrases =
    [
        "Apple",
        "Banana is yellow",
        "Cherry is the best",
        "Something something something",
    ];

    private readonly Random _random;
    private readonly string[] _phrases;
    private readonly int _maxNumber;

    public LineFactory(int uniqueStringCount, int maxNumber, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(uniqueStringCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNumber, 1);

        _maxNumber = maxNumber;
        _random = new Random(seed);
        _phrases = new string[uniqueStringCount];

        for (int i = 0; i < uniqueStringCount; i++)
        {
            string basePhrase = BasePhrases[i % BasePhrases.Length];
            _phrases[i] = i < BasePhrases.Length
                ? basePhrase
                : $"{basePhrase} {i}";
        }
    }

    public GeneratedLine Next()
    {
        int number = _random.Next(_maxNumber) + 1;
        string text = _phrases[_random.Next(_phrases.Length)];
        return new GeneratedLine(number, text);
    }
}
