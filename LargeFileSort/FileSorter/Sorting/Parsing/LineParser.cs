namespace FileSorter.Sorting.Parsing;

public static class LineParser
{
    public const int NumberTextSeparatorLength = 2;

    public static LineRef Parse(ReadOnlySpan<byte> line) => Parse(line, start: 0, end: line.Length);

    public static LineRef Parse(ReadOnlySpan<byte> buffer, int start, int end)
    {
        if ((uint)start > (uint)buffer.Length || (uint)end > (uint)buffer.Length || start > end)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buffer),
                "Line range is outside the buffer.");
        }

        ReadOnlySpan<byte> line = buffer[start..end];
        if (line.IsEmpty)
        {
            throw new FormatException("Line is empty.");
        }

        int index = 0;
        while (index < line.Length && IsDigit(line[index]))
        {
            index++;
        }

        if (index == 0)
        {
            throw new FormatException("Line must start with a decimal number.");
        }

        if (index >= line.Length || line[index] != (byte)'.')
        {
            throw new FormatException("Expected '.' immediately after the number.");
        }

        index++;
        if (index >= line.Length || line[index] != (byte)' ')
        {
            throw new FormatException("Expected a single space after '.'.");
        }

        index++;
        if (ContainsLineBreak(line[index..]))
        {
            throw new FormatException("Line must not contain CR or LF.");
        }

        return new LineRef(start, textStart: start + index, end);
    }

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool ContainsLineBreak(ReadOnlySpan<byte> text) =>
        text.IndexOfAny((byte)'\n', (byte)'\r') >= 0;
}
