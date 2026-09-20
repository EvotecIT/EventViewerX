using System.Text;

namespace EventViewerX.Evtx;

/// <summary>Reads process output one bounded line at a time without materializing an unlimited line first.</summary>
internal sealed class EvtxDumpBoundedLineReader {
    private const int BufferSize = 64 * 1024;

    private readonly StreamReader _reader;
    private readonly int _maximumLineCharacters;
    private readonly char[] _buffer = new char[BufferSize];
    private int _position;
    private int _length;
    private bool _skipLineFeed;

    internal EvtxDumpBoundedLineReader(StreamReader reader, int maximumLineCharacters) {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        if (maximumLineCharacters <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumLineCharacters));
        }
        _maximumLineCharacters = maximumLineCharacters;
    }

    internal string? ReadLine(Action waitBoundaryCheck) {
        if (waitBoundaryCheck == null) {
            throw new ArgumentNullException(nameof(waitBoundaryCheck));
        }

        StringBuilder? line = null;
        while (true) {
            if (_position >= _length) {
                Task<int> read = _reader.ReadAsync(_buffer, 0, _buffer.Length);
                while (!read.Wait(100)) {
                    waitBoundaryCheck();
                }
                _length = read.GetAwaiter().GetResult();
                waitBoundaryCheck();
                _position = 0;
                if (_length == 0) {
                    return line?.ToString();
                }
            }

            if (_skipLineFeed) {
                _skipLineFeed = false;
                if (_buffer[_position] == '\n') {
                    _position++;
                    continue;
                }
            }

            int segmentStart = _position;
            while (_position < _length &&
                   _buffer[_position] != '\r' &&
                   _buffer[_position] != '\n') {
                _position++;
            }
            int segmentLength = _position - segmentStart;
            int existingLength = line?.Length ?? 0;
            if (existingLength > _maximumLineCharacters - segmentLength) {
                throw new InvalidDataException(
                    $"evtx_dump produced an output line larger than {_maximumLineCharacters} characters.");
            }

            if (_position < _length && line == null) {
                string result = new(_buffer, segmentStart, segmentLength);
                ConsumeDelimiter();
                return result;
            }
            if (segmentLength > 0) {
                line ??= new StringBuilder(Math.Min(_maximumLineCharacters, BufferSize));
                line.Append(_buffer, segmentStart, segmentLength);
            }
            if (_position < _length) {
                ConsumeDelimiter();
                return line?.ToString() ?? string.Empty;
            }
        }
    }

    private void ConsumeDelimiter() {
        char delimiter = _buffer[_position++];
        _skipLineFeed = delimiter == '\r';
    }
}
