namespace FileSorter.Sorting.Parsing;

public readonly struct LineRef
{
    public LineRef(int start, int textStart, int end)
    {
        Start = start;
        TextStart = textStart;
        End = end;
    }

    public int Start { get; }
    public int TextStart { get; }
    public int End { get; }

    public ReadOnlySpan<byte> Number(ReadOnlySpan<byte> buffer) =>
        buffer.Slice(Start, TextStart - LineParser.NumberTextSeparatorLength - Start);

    public ReadOnlySpan<byte> Text(ReadOnlySpan<byte> buffer) =>
        buffer.Slice(TextStart, End - TextStart);
}
