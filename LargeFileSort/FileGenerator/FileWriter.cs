using System.Text;

namespace FileGenerator;

public sealed class FileWriter
{
    public const int MinLineLength = 5;

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
            string body = factory.Next().ToFileText();
            int fullLength = Encoding.UTF8.GetByteCount(body) + 1;
            long leftoverAfter = remaining - fullLength;

            if (leftoverAfter == 0)
            {
                WriteLine(stream, body);
                return;
            }

            if (leftoverAfter >= MinLineLength)
            {
                WriteLine(stream, body);
                written += fullLength;
                continue;
            }

            if (leftoverAfter > 0)
            {
                WriteLine(stream, body + new string(' ', (int)leftoverAfter));
                return;
            }

            WriteFittedLine(stream, body, (int)remaining);
            return;
        }
    }

    private static void WriteFittedLine(FileStream stream, string body, int remaining)
    {
        int bodyBytes = remaining - 1;
        int currentBytes = Encoding.UTF8.GetByteCount(body);

        if (currentBytes <= bodyBytes)
        {
            WriteLine(stream, body + new string(' ', bodyBytes - currentBytes));
            return;
        }

        int textLength = bodyBytes - 3;
        WriteLine(stream, "1. " + new string('x', textLength));
    }

    private static void WriteLine(FileStream stream, string body)
    {
        stream.Write(Encoding.UTF8.GetBytes(body));
        stream.WriteByte((byte)'\n');
    }
}
