using FileSorter.Sorting.Parsing;

namespace FileSorter.Sorting.Merging;

public sealed class RunReader : IDisposable
{
    private readonly ChunkReader _reader;
    private int _index = -1;

    public RunReader(string path, int bufferSize, int maxLineLength)
    {
        FileStream stream = File.OpenRead(path);
        try
        {
            _reader = new ChunkReader(stream, bufferSize, maxLineLength);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public RunReader(Stream stream, int bufferSize, int maxLineLength, bool leaveOpen = false)
    {
        _reader = new ChunkReader(stream, bufferSize, maxLineLength, leaveOpen);
    }

    public byte[] Buffer => _reader.Buffer;

    public LineRef Current
    {
        get
        {
            if ((uint)_index >= (uint)_reader.Lines.Length)
            {
                throw new InvalidOperationException("RunReader has no current line.");
            }

            return _reader.Lines[_index];
        }
    }

    public bool MoveNext()
    {
        if (_index >= 0 && _index + 1 < _reader.Lines.Length)
        {
            _index++;
            return true;
        }

        if (!_reader.MoveNextChunk() || _reader.Lines.Length == 0)
        {
            _index = -1;
            return false;
        }

        _index = 0;
        return true;
    }

    public void Dispose() => _reader.Dispose();
}
