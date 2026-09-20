namespace EventViewerX.Evtx;

/// <summary>Correlates parser output with the original EVTX record header without indexing the whole file.</summary>
internal sealed class EvtxRecordHeaderCursor : IDisposable {
    private const int FileHeaderSize = 4096;
    private const int ChunkSize = 65536;
    private const int ChunkHeaderSize = 512;
    private const int RecordHeaderSize = 24;
    private const int RecordTrailerSize = 4;
    private const long ChunkSignature = 0x006B6E6843666C45;

    private readonly IEnumerator<EvtxRecordHeader> _headers;
    private bool _completed;

    internal EvtxRecordHeaderCursor(string path, CancellationToken cancellationToken) {
        _headers = Read(path, cancellationToken).GetEnumerator();
    }

    internal bool TryApply(SavedEventRecord record) {
        if (_completed || !record.RecordId.HasValue) {
            return false;
        }
        while (_headers.MoveNext()) {
            EvtxRecordHeader header = _headers.Current;
            if (header.RecordNumber != record.RecordId.Value) {
                continue;
            }
            record.FileOffset = header.FileOffset;
            return true;
        }
        _completed = true;
        return false;
    }

    public void Dispose() => _headers.Dispose();

    private static IEnumerable<EvtxRecordHeader> Read(
        string path,
        CancellationToken cancellationToken) {

        using FileStream stream = File.Open(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        long payloadLength = Math.Max(0, stream.Length - FileHeaderSize);
        int chunks = checked((int)((payloadLength + ChunkSize - 1) / ChunkSize));
        var buffer = new byte[ChunkSize];
        for (int chunkIndex = 0; chunkIndex < chunks; chunkIndex++) {
            cancellationToken.ThrowIfCancellationRequested();
            long chunkFileOffset = FileHeaderSize + (long)chunkIndex * ChunkSize;
            stream.Position = chunkFileOffset;
            int chunkLength = ReadAvailable(stream, buffer, 0, ChunkSize);
            if (chunkLength < ChunkHeaderSize || BitConverter.ToInt64(buffer, 0) != ChunkSignature) {
                continue;
            }

            uint freeSpaceValue = BitConverter.ToUInt32(buffer, 0x30);
            if (freeSpaceValue < ChunkHeaderSize || freeSpaceValue > chunkLength) {
                continue;
            }
            int chunkEnd = (int)freeSpaceValue;
            int recordOffset = ChunkHeaderSize;
            while (recordOffset + RecordHeaderSize + RecordTrailerSize <= chunkEnd) {
                cancellationToken.ThrowIfCancellationRequested();
                if (BitConverter.ToInt32(buffer, recordOffset) != 0x00002A2A) {
                    break;
                }
                uint recordSizeValue = BitConverter.ToUInt32(buffer, recordOffset + 4);
                if (recordSizeValue > int.MaxValue) {
                    break;
                }
                int recordSize = (int)recordSizeValue;
                if (recordSize < RecordHeaderSize + RecordTrailerSize ||
                    recordSize > chunkEnd - recordOffset) {
                    break;
                }
                int trailerOffset = recordOffset + recordSize - RecordTrailerSize;
                if (BitConverter.ToUInt32(buffer, trailerOffset) != recordSizeValue) {
                    break;
                }

                long recordNumber = BitConverter.ToInt64(buffer, recordOffset + 8);
                long fileTime = BitConverter.ToInt64(buffer, recordOffset + 16);
                try {
                    _ = DateTime.FromFileTimeUtc(fileTime);
                } catch (ArgumentOutOfRangeException) {
                    recordOffset += recordSize;
                    continue;
                }
                yield return new EvtxRecordHeader(
                    recordNumber,
                    chunkFileOffset + recordOffset);
                recordOffset += recordSize;
            }
        }
    }

    private static int ReadAvailable(Stream stream, byte[] buffer, int offset, int count) {
        int total = 0;
        while (total < count) {
            int read = stream.Read(buffer, offset + total, count - total);
            if (read == 0) {
                break;
            }
            total += read;
        }
        return total;
    }

    private sealed class EvtxRecordHeader {
        internal EvtxRecordHeader(long recordNumber, long fileOffset) {
            RecordNumber = recordNumber;
            FileOffset = fileOffset;
        }

        internal long RecordNumber { get; }
        internal long FileOffset { get; }
    }
}
