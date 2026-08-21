using System.Buffers.Binary;

namespace EventViewerX.Providers;

/// <summary>
/// Provides checked little-endian binary construction with explicit offset
/// patching for Windows event metadata and PE resources.
/// </summary>
internal sealed class EventProviderBinaryBuffer : IDisposable {
    private readonly MemoryStream _stream = new();

    internal int Position => checked((int)_stream.Position);

    internal void WriteByte(byte value) {
        _stream.WriteByte(value);
    }

    internal void WriteBytes(ReadOnlySpan<byte> value) {
#if NET472
        byte[] bytes = value.ToArray();
        _stream.Write(bytes, 0, bytes.Length);
#else
        _stream.Write(value);
#endif
    }

    internal void WriteAscii(string value) {
        WriteBytes(Encoding.ASCII.GetBytes(value));
    }

    internal void WriteUInt16(ushort value) {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        WriteBytes(bytes);
    }

    internal void WriteUInt32(uint value) {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        WriteBytes(bytes);
    }

    internal void WriteUInt64(ulong value) {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        WriteBytes(bytes);
    }

    internal void WriteGuid(Guid value) {
        WriteBytes(value.ToByteArray());
    }

    internal int ReserveUInt16() {
        int offset = Position;
        WriteUInt16(0);
        return offset;
    }

    internal int ReserveUInt32() {
        int offset = Position;
        WriteUInt32(0);
        return offset;
    }

    internal void PatchUInt16(int offset, ushort value) {
        if (offset < 0 || offset > Position - sizeof(ushort)) {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        long original = _stream.Position;
        _stream.Position = offset;
        WriteUInt16(value);
        _stream.Position = original;
    }

    internal void PatchUInt32(int offset, uint value) {
        if (offset < 0 || offset > Position - sizeof(uint)) {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        long original = _stream.Position;
        _stream.Position = offset;
        WriteUInt32(value);
        _stream.Position = original;
    }

    internal void Align(int alignment) {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0) {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }
        while ((Position & (alignment - 1)) != 0) {
            WriteByte(0);
        }
    }

    internal void WriteUtf16(string value, bool nullTerminate) {
        WriteBytes(Encoding.Unicode.GetBytes(value));
        if (nullTerminate) {
            WriteUInt16(0);
        }
    }

    internal void WriteSizedUtf16(string value, int alignment = 4) {
        int start = Position;
        int sizeOffset = ReserveUInt32();
        WriteUtf16(value, nullTerminate: true);
        Align(alignment);
        PatchUInt32(sizeOffset, checked((uint)(Position - start)));
    }

    internal byte[] ToArray() {
        return _stream.ToArray();
    }

    public void Dispose() {
        _stream.Dispose();
    }
}
