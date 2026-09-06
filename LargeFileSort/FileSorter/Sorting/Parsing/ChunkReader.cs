namespace FileSorter.Sorting.Parsing;

public sealed class ChunkReader : IDisposable
{
    public const int DefaultBufferSize = 64 * 1024 * 1024;
    public const int DefaultMaxLineLength = 1024 * 1024;

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly Stream _stream;
    private readonly int _maxLineLength;
    private readonly bool _leaveOpen;

    private byte[] _buffer;
    private LineRef[] _lines = new LineRef[16];
    private int _filled;
    private int _consumed;
    private int _lineCount;
    private int _lineNumber;
    private long _bufferFileOffset;
    private bool _bomChecked;
    private bool _eof;
    private bool _disposed;

    public ChunkReader(Stream stream, int bufferSize, int maxLineLength, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLineLength, 1);

        _stream = stream;
        _maxLineLength = maxLineLength;
        _leaveOpen = leaveOpen;
        _buffer = new byte[Math.Min(bufferSize, maxLineLength)];
    }

    public byte[] Buffer => _buffer;

    public ReadOnlySpan<LineRef> Lines => _lines.AsSpan(0, _lineCount);

    public bool MoveNextChunk()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CompactTail();
        _lineCount = 0;

        SkipBomIfNeeded();

        while (true)
        {
            FillBuffer();
            _consumed = ParseCompleteLines();

            if (_eof && _filled > _consumed)
            {
                AddParsedLine(_consumed, _filled);
                _consumed = _filled;
            }

            if (_lineCount > 0)
            {
                return true;
            }

            if (_eof)
            {
                return false;
            }

            GrowBuffer();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private void SkipBomIfNeeded()
    {
        if (_bomChecked)
        {
            return;
        }

        _bomChecked = true;

        while (_filled < Utf8Bom.Length && !_eof)
        {
            if (_filled == _buffer.Length)
            {
                GrowBuffer();
            }

            FillBuffer();
        }

        if (_filled >= Utf8Bom.Length
            && _buffer[0] == Utf8Bom[0]
            && _buffer[1] == Utf8Bom[1]
            && _buffer[2] == Utf8Bom[2])
        {
            int remaining = _filled - Utf8Bom.Length;
            if (remaining > 0)
            {
                System.Buffer.BlockCopy(_buffer, Utf8Bom.Length, _buffer, 0, remaining);
            }

            _filled = remaining;
            _bufferFileOffset += Utf8Bom.Length;
        }
    }

    private void FillBuffer()
    {
        while (_filled < _buffer.Length && !_eof)
        {
            int read = _stream.Read(_buffer, _filled, _buffer.Length - _filled);
            if (read == 0)
            {
                _eof = true;
                return;
            }

            _filled += read;
        }

        if (!_eof && _stream.CanSeek && _stream.Position >= _stream.Length)
        {
            _eof = true;
        }
    }

    private int ParseCompleteLines()
    {
        int position = 0;

        while (position < _filled)
        {
            int newline = Array.IndexOf(_buffer, (byte)'\n', position, _filled - position);
            if (newline < 0)
            {
                return position;
            }

            int contentEnd = newline;
            if (contentEnd > position && _buffer[contentEnd - 1] == (byte)'\r')
            {
                contentEnd--;
            }

            AddParsedLine(position, contentEnd);
            position = newline + 1;
        }

        return position;
    }

    private void AddParsedLine(int start, int end)
    {
        _lineNumber++;

        LineRef line;
        try
        {
            line = LineParser.Parse(_buffer, start, end);
        }
        catch (FormatException ex)
        {
            throw new FormatException(
                $"Invalid line {_lineNumber} at byte offset {_bufferFileOffset + start}: {ex.Message}",
                ex);
        }

        if (_lineCount == _lines.Length)
        {
            Array.Resize(ref _lines, _lines.Length * 2);
        }

        _lines[_lineCount++] = line;
    }

    private void CompactTail()
    {
        if (_consumed == 0)
        {
            return;
        }

        int tail = _filled - _consumed;
        if (tail > 0)
        {
            System.Buffer.BlockCopy(_buffer, _consumed, _buffer, 0, tail);
        }

        _filled = tail;
        _bufferFileOffset += _consumed;
        _consumed = 0;
    }

    private void GrowBuffer()
    {
        if (_buffer.Length >= _maxLineLength)
        {
            throw new FormatException(
                $"Line exceeds max length {_maxLineLength} at byte offset {_bufferFileOffset}.");
        }

        int newSize = Math.Min(_maxLineLength, _buffer.Length * 2);
        byte[] grown = new byte[newSize];
        System.Buffer.BlockCopy(_buffer, 0, grown, 0, _filled);
        _buffer = grown;
    }
}
