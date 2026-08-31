using System.Security;
using System.Text;

namespace EventViewerX.Evtx;

internal static class EvtxLiteralRecordReader {
    private const int FileHeaderSize = 4096;
    private const int ChunkSize = 65536;
    private const int ChunkHeaderSize = 512;
    private const int RecordHeaderSize = 24;
    private const int RecordTrailerSize = 4;
    private const int MaximumDepth = 128;
    private const int MaximumTokensPerRecord = 100_000;
    private const long ChunkSignature = 0x006B6E6843666C45;

    internal static bool IsLiteralBinXml(Stream stream) {
        long originalPosition = stream.Position;
        try {
            var header = new byte[ChunkHeaderSize + RecordHeaderSize + 5];
            long payloadLength = Math.Max(0, stream.Length - FileHeaderSize);
            long chunks = (payloadLength + ChunkSize - 1) / ChunkSize;
            for (long chunkIndex = 0; chunkIndex < chunks; chunkIndex++) {
                stream.Position = FileHeaderSize + chunkIndex * ChunkSize;
                int read = ReadAvailable(stream, header, 0, header.Length);
                if (read < header.Length || BitConverter.ToInt64(header, 0) != ChunkSignature ||
                    BitConverter.ToInt32(header, ChunkHeaderSize) != 0x00002A2A) {
                    continue;
                }

                int binXml = ChunkHeaderSize + RecordHeaderSize;
                if (header[binXml] != 0x0F) {
                    return false;
                }

                byte rootToken = header[binXml + 4];
                return rootToken == 0x01 || rootToken == 0x41;
            }
            return false;
        } finally {
            stream.Position = originalPosition;
        }
    }

    internal static IEnumerable<LiteralEvtxRecord> Read(
        Stream stream,
        CancellationToken cancellationToken) {

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
            uint lastRecordValue = BitConverter.ToUInt32(buffer, 0x2C);
            if (freeSpaceValue < ChunkHeaderSize || freeSpaceValue > chunkLength ||
                (lastRecordValue != 0 &&
                 (lastRecordValue < ChunkHeaderSize ||
                  lastRecordValue > freeSpaceValue - RecordHeaderSize - RecordTrailerSize))) {
                throw new InvalidDataException(
                    $"Literal BinXML chunk at file offset 0x{chunkFileOffset:X} has inconsistent record and free-space offsets.");
            }
            int chunkEnd = (int)freeSpaceValue;
            int recordOffset = ChunkHeaderSize;
            var names = new Dictionary<uint, EvtxBinXmlName>();
            while (recordOffset + RecordHeaderSize + RecordTrailerSize <= chunkEnd) {
                cancellationToken.ThrowIfCancellationRequested();
                if (BitConverter.ToInt32(buffer, recordOffset) != 0x00002A2A) {
                    throw new InvalidDataException(
                        $"Literal BinXML record at file offset 0x{chunkFileOffset + recordOffset:X} has an invalid record signature before the declared free-space offset.");
                }

                int recordSize = checked((int)BitConverter.ToUInt32(buffer, recordOffset + 4));
                if (recordSize < RecordHeaderSize + RecordTrailerSize ||
                    recordOffset + recordSize > chunkEnd) {
                    throw new InvalidDataException(
                        $"Literal BinXML record at file offset 0x{chunkFileOffset + recordOffset:X} has an invalid size.");
                }

                long recordNumber = BitConverter.ToInt64(buffer, recordOffset + 8);
                long fileTime = BitConverter.ToInt64(buffer, recordOffset + 16);
                DateTime timestampUtc;
                try {
                    timestampUtc = DateTime.FromFileTimeUtc(fileTime);
                } catch (ArgumentOutOfRangeException exception) {
                    throw new InvalidDataException(
                        $"Literal BinXML record {recordNumber} has an invalid FILETIME.", exception);
                }

                int payloadStart = recordOffset + RecordHeaderSize;
                int payloadEnd = recordOffset + recordSize - RecordTrailerSize;
                int trailingSize = checked((int)BitConverter.ToUInt32(buffer, payloadEnd));
                if (trailingSize != recordSize) {
                    throw new InvalidDataException(
                        $"Literal BinXML record {recordNumber} has mismatched header and trailer sizes.");
                }

                var cursor = new EvtxBinXmlCursor(buffer, chunkLength, payloadStart, payloadEnd, names);
                string xml = cursor.ReadDocument();
                yield return new LiteralEvtxRecord(
                    recordNumber,
                    timestampUtc,
                    chunkFileOffset + recordOffset,
                    xml);
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

    internal sealed class LiteralEvtxRecord {
        internal LiteralEvtxRecord(
            long recordNumber,
            DateTime timestampUtc,
            long fileOffset,
            string xml) {

            RecordNumber = recordNumber;
            TimestampUtc = timestampUtc;
            FileOffset = fileOffset;
            Xml = xml;
        }

        internal long RecordNumber { get; }
        internal DateTime TimestampUtc { get; }
        internal long FileOffset { get; }
        internal string Xml { get; }
    }

    private sealed class EvtxBinXmlCursor {
        private readonly byte[] _chunk;
        private readonly int _chunkLength;
        private readonly int _end;
        private readonly Dictionary<uint, EvtxBinXmlName> _names;
        private int _position;
        private int _tokens;

        internal EvtxBinXmlCursor(
            byte[] chunk,
            int chunkLength,
            int start,
            int end,
            Dictionary<uint, EvtxBinXmlName> names) {

            _chunk = chunk;
            _chunkLength = chunkLength;
            _position = start;
            _end = end;
            _names = names;
        }

        internal string ReadDocument() {
            Require(5);
            if (ReadByte() != 0x0F) {
                throw Error("The record does not start with a BinXML fragment header.");
            }
            ReadByte();
            ReadByte();
            ReadByte();

            var builder = new StringBuilder(Math.Min(16_384, _end - _position));
            ReadElement(builder, 0);
            if (_position < _end && PeekByte() == 0x00) {
                ReadByte();
            }
            return builder.ToString();
        }

        private void ReadElement(StringBuilder builder, int depth) {
            CountToken();
            if (depth >= MaximumDepth) {
                throw Error($"BinXML nesting exceeds {MaximumDepth} elements.");
            }

            byte token = ReadByte();
            if (token != 0x01 && token != 0x41) {
                throw Error($"Expected an open-element token but found 0x{token:X2}.");
            }

            int layoutStart = _position;
            uint elementLength = ReadUInt32();
            int contentStart = _position;
            if (!IsBoundedLength(contentStart, elementLength)) {
                _position = layoutStart;
                Require(2);
                _position += 2;
                elementLength = ReadUInt32();
                contentStart = _position;
                if (!IsBoundedLength(contentStart, elementLength)) {
                    throw Error("The open-element length is outside the record.");
                }
            }
            int elementEnd = checked(contentStart + (int)elementLength);

            EvtxBinXmlName name = ReadNameReference();
            builder.Append('<').Append(name.Value);
            if (token == 0x41) {
                uint attributeBytes = ReadUInt32();
                int attributeEnd = checked(_position + (int)attributeBytes);
                if (attributeEnd > elementEnd) {
                    throw Error("The attribute list extends beyond its element.");
                }
                while (_position < attributeEnd) {
                    ReadAttribute(builder, attributeEnd);
                }
                if (_position != attributeEnd) {
                    throw Error("The attribute list ended at an unexpected offset.");
                }
            }

            byte close = ReadByte();
            if (close == 0x03) {
                builder.Append(" />");
                _position = elementEnd;
                return;
            }
            if (close != 0x02) {
                throw Error($"Expected an element-close token but found 0x{close:X2}.");
            }
            builder.Append('>');

            while (_position < elementEnd) {
                byte next = PeekByte();
                if (next == 0x04) {
                    CountToken();
                    ReadByte();
                    builder.Append("</").Append(name.Value).Append('>');
                    _position = elementEnd;
                    return;
                }
                if (next == 0x01 || next == 0x41) {
                    ReadElement(builder, depth + 1);
                    continue;
                }
                if (next == 0x00) {
                    throw Error($"Element {name.Value} ended before its close token.");
                }
                ReadCharacterData(builder);
            }
            throw Error($"Element {name.Value} has no end-element token.");
        }

        private void ReadAttribute(StringBuilder builder, int attributeEnd) {
            CountToken();
            byte token = ReadByte();
            if (token != 0x06 && token != 0x46) {
                throw Error($"Expected an attribute token but found 0x{token:X2}.");
            }
            EvtxBinXmlName name = ReadNameReference();
            var value = new StringBuilder();
            bool more;
            do {
                if (_position >= attributeEnd) {
                    throw Error($"Attribute {name.Value} has no value.");
                }
                byte valueToken = PeekByte();
                more = (valueToken & 0x40) != 0;
                ReadCharacterData(value);
            } while (more);
            builder.Append(' ').Append(name.Value).Append("=\"")
                .Append(value).Append('"');
        }

        private void ReadCharacterData(StringBuilder builder) {
            CountToken();
            byte token = ReadByte();
            switch (token & 0x0F) {
                case 0x05:
                    byte valueType = ReadByte();
                    if (valueType != 0x01) {
                        throw Error($"Literal text uses unsupported BinXML value type 0x{valueType:X2}.");
                    }
                    ushort characters = ReadUInt16();
                    string value = ReadUnicode(characters);
                    builder.Append(SecurityElement.Escape(value));
                    break;
                case 0x08:
                    ushort codePoint = ReadUInt16();
                    builder.Append("&#").Append(codePoint).Append(';');
                    break;
                case 0x09:
                    EvtxBinXmlName entity = ReadNameReference();
                    if (entity.Value != "quot" && entity.Value != "apos" && entity.Value != "amp" &&
                        entity.Value != "lt" && entity.Value != "gt") {
                        throw Error($"Unsupported XML entity reference &{entity.Value};.");
                    }
                    builder.Append('&').Append(entity.Value).Append(';');
                    break;
                default:
                    throw Error($"Unsupported literal BinXML token 0x{token:X2}.");
            }
        }

        private EvtxBinXmlName ReadNameReference() {
            uint offset = ReadUInt32();
            if (!_names.TryGetValue(offset, out EvtxBinXmlName? name)) {
                if (offset > int.MaxValue) {
                    throw Error($"BinXML name offset 0x{offset:X} is outside its chunk.");
                }
                int nameOffset = (int)offset;
                if (nameOffset > _chunkLength - 10) {
                    throw Error($"BinXML name offset 0x{offset:X} is outside its chunk.");
                }
                ushort characters = BitConverter.ToUInt16(_chunk, nameOffset + 6);
                int size = checked(10 + characters * 2);
                if (nameOffset > _chunkLength - size) {
                    throw Error($"BinXML name at 0x{offset:X} is truncated.");
                }
                string value = Encoding.Unicode.GetString(_chunk, nameOffset + 8, characters * 2);
                name = new EvtxBinXmlName(value, size);
                _names.Add(offset, name);
            }
            if (offset == _position) {
                Require(name.Size);
                _position += name.Size;
            }
            return name;
        }

        private bool IsBoundedLength(int start, uint length) =>
            length <= int.MaxValue && start <= _end - (int)length;

        private byte PeekByte() {
            Require(1);
            return _chunk[_position];
        }

        private byte ReadByte() {
            Require(1);
            return _chunk[_position++];
        }

        private ushort ReadUInt16() {
            Require(2);
            ushort value = BitConverter.ToUInt16(_chunk, _position);
            _position += 2;
            return value;
        }

        private uint ReadUInt32() {
            Require(4);
            uint value = BitConverter.ToUInt32(_chunk, _position);
            _position += 4;
            return value;
        }

        private string ReadUnicode(int characters) {
            int bytes = checked(characters * 2);
            Require(bytes);
            string value = Encoding.Unicode.GetString(_chunk, _position, bytes);
            _position += bytes;
            return value;
        }

        private void CountToken() {
            _tokens++;
            if (_tokens > MaximumTokensPerRecord) {
                throw Error($"BinXML record exceeds {MaximumTokensPerRecord} tokens.");
            }
        }

        private void Require(int bytes) {
            if (bytes < 0 || _position > _end - bytes) {
                throw Error("BinXML data is truncated.");
            }
        }

        private InvalidDataException Error(string message) =>
            new($"{message} Chunk offset 0x{_position:X}.");
    }

    private sealed class EvtxBinXmlName {
        internal EvtxBinXmlName(string value, int size) {
            Value = value;
            Size = size;
        }

        internal string Value { get; }
        internal int Size { get; }
    }
}
