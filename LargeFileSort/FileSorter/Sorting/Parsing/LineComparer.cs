namespace FileSorter.Sorting.Parsing;

public readonly struct LineComparer : IComparer<LineRef>
{
    private readonly byte[] _buffer;

    public LineComparer(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
    }

    public int Compare(LineRef x, LineRef y) => Compare(_buffer, x, _buffer, y);

    public static int Compare(ReadOnlySpan<byte> buffer, LineRef left, LineRef right) =>
        Compare(buffer, left, buffer, right);

    public static int CompareLines(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return Compare(left, LineParser.Parse(left), right, LineParser.Parse(right));
    }

    public static int Compare(
        ReadOnlySpan<byte> leftBuffer,
        LineRef left,
        ReadOnlySpan<byte> rightBuffer,
        LineRef right)
    {
        int textOrder = left.Text(leftBuffer).SequenceCompareTo(right.Text(rightBuffer));
        if (textOrder != 0)
        {
            return textOrder;
        }

        ReadOnlySpan<byte> leftNumber = left.Number(leftBuffer);
        ReadOnlySpan<byte> rightNumber = right.Number(rightBuffer);

        int numericOrder = CompareNumeric(leftNumber, rightNumber);
        if (numericOrder != 0)
        {
            return numericOrder;
        }

        return leftNumber.SequenceCompareTo(rightNumber);
    }

    private static int CompareNumeric(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        ReadOnlySpan<byte> leftSignificant = StripLeadingZeros(left);
        ReadOnlySpan<byte> rightSignificant = StripLeadingZeros(right);

        if (leftSignificant.Length != rightSignificant.Length)
        {
            return leftSignificant.Length.CompareTo(rightSignificant.Length);
        }

        return leftSignificant.SequenceCompareTo(rightSignificant);
    }

    private static ReadOnlySpan<byte> StripLeadingZeros(ReadOnlySpan<byte> digits)
    {
        int index = 0;
        while (index < digits.Length - 1 && digits[index] == (byte)'0')
        {
            index++;
        }

        return digits[index..];
    }
}
