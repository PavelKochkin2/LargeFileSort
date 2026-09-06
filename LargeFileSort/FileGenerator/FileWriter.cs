using System.Buffers.Text;
using System.Text;

namespace FileGenerator;

public sealed class FileWriter
{
    public const int MinLineLength = 5;

    private byte[] _buffer = new byte[256];

    public void Write(string path, long sizeInBytes, LineFactory factory)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes);

        if (sizeInBytes == 0)
        {
            File.WriteAllBytes(path, []);
            return;
        }

        if (sizeInBytes < MinLineLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeInBytes),
                sizeInBytes,
                $"File size must be 0 or at least {MinLineLength} bytes.");
        }

        using FileStream stream = File.Create(path);
        long written = 0;

        while (written < sizeInBytes)
        {
            long remaining = sizeInBytes - written;
            GeneratedLine line = factory.Next();
            int fullLength = Encode(line, padSpaces: 0);
            long leftoverAfter = remaining - fullLength;

            if (leftoverAfter == 0)
            {
                stream.Write(_buffer, 0, fullLength);
                return;
            }

            if (leftoverAfter >= MinLineLength)
            {
                stream.Write(_buffer, 0, fullLength);
                written += fullLength;
                continue;
            }

            if (leftoverAfter > 0)
            {
                int paddedLength = Encode(line, padSpaces: (int)leftoverAfter);
                stream.Write(_buffer, 0, paddedLength);
                return;
            }

            int fittedLength = EncodeFitted(line, (int)remaining);
            stream.Write(_buffer, 0, fittedLength);
            return;
        }
    }

    private int Encode(GeneratedLine line, int padSpaces)
    {
        int textBytes = Encoding.UTF8.GetByteCount(line.Text);
        EnsureCapacity(11 + 2 + textBytes + padSpaces + 1);

        if (!Utf8Formatter.TryFormat(line.Number, _buffer, out int written))
        {
            throw new InvalidOperationException("Failed to format line number.");
        }

        _buffer[written++] = (byte)'.';
        _buffer[written++] = (byte)' ';
        written += Encoding.UTF8.GetBytes(line.Text, _buffer.AsSpan(written));
        _buffer.AsSpan(written, padSpaces).Fill((byte)' ');
        written += padSpaces;
        _buffer[written++] = (byte)'\n';
        return written;
    }

    private int EncodeFitted(GeneratedLine line, int remaining)
    {
        EnsureCapacity(remaining);

        Span<byte> numberBytes = stackalloc byte[11];
        if (!Utf8Formatter.TryFormat(line.Number, numberBytes, out int numberLength))
        {
            throw new InvalidOperationException("Failed to format line number.");
        }

        int prefixLength = numberLength + 2;
        int textBudget = remaining - 1 - prefixLength;

        if (textBudget >= 1)
        {
            numberBytes[..numberLength].CopyTo(_buffer);
            int written = numberLength;
            _buffer[written++] = (byte)'.';
            _buffer[written++] = (byte)' ';
            written += WriteText(_buffer.AsSpan(written, textBudget), line.Text);
            _buffer[written++] = (byte)'\n';
            return written;
        }

        const int fallbackPrefixLength = 3;
        int fill = remaining - fallbackPrefixLength - 1;
        _buffer[0] = (byte)'1';
        _buffer[1] = (byte)'.';
        _buffer[2] = (byte)' ';
        int filled = fallbackPrefixLength + WriteText(_buffer.AsSpan(fallbackPrefixLength, fill), line.Text);
        _buffer[filled++] = (byte)'\n';
        return filled;
    }

    private static int WriteText(Span<byte> dest, string text)
    {
        int textBytes = Encoding.UTF8.GetByteCount(text);
        if (textBytes <= dest.Length)
        {
            Encoding.UTF8.GetBytes(text, dest);
            dest[textBytes..].Fill((byte)' ');
            return dest.Length;
        }

        int charsToCopy = dest.Length;
        Encoding.UTF8.GetBytes(text.AsSpan(0, charsToCopy), dest);
        return dest.Length;
    }

    private void EnsureCapacity(int needed)
    {
        if (_buffer.Length >= needed)
        {
            return;
        }

        int newSize = _buffer.Length;
        while (newSize < needed)
        {
            newSize *= 2;
        }

        _buffer = new byte[newSize];
    }
}
