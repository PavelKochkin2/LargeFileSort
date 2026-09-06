namespace FileGenerator.Cli;

public static class ByteSize
{
    public static long Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        int digitCount = 0;
        while (digitCount < span.Length && char.IsAsciiDigit(span[digitCount]))
        {
            digitCount++;
        }

        if (digitCount == 0 || !long.TryParse(span[..digitCount], out long number))
        {
            throw new FormatException($"'{value}' is not a valid size. Examples: 1024, 500KB, 10MB, 1GB.");
        }

        string suffix = span[digitCount..].Trim().ToString().ToUpperInvariant();
        long multiplier = suffix switch
        {
            "" or "B" => 1,
            "K" or "KB" => 1024L,
            "M" or "MB" => 1024L * 1024,
            "G" or "GB" => 1024L * 1024 * 1024,
            "T" or "TB" => 1024L * 1024 * 1024 * 1024,
            _ => throw new FormatException($"'{value}' is not a valid size. Examples: 1024, 500KB, 10MB, 1GB."),
        };

        try
        {
            return checked(number * multiplier);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Size is too large.");
        }
    }
}
