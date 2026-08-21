using System.Buffers;
using System.IO;
using System.Text;

namespace EventViewerX.Exports;

internal sealed class EventXmlWriter : IDisposable {
    private static readonly byte[] Header =
        Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><Events>");
    private static readonly byte[] Footer = Encoding.UTF8.GetBytes("</Events>");
    private readonly Stream _stream;
    private byte[]? _utf8Buffer;
    private bool _completed;

    internal EventXmlWriter(Stream stream) {
        _stream = stream;
        _stream.Write(Header, 0, Header.Length);
    }

    internal Stream EventStream => _stream;

    internal void WriteXml(string xml) {
        if (string.IsNullOrEmpty(xml)) {
            throw new InvalidOperationException("The event did not contain raw XML.");
        }

        int required = Encoding.UTF8.GetMaxByteCount(xml.Length);
        if (_utf8Buffer == null || _utf8Buffer.Length < required) {
            if (_utf8Buffer != null) {
                ArrayPool<byte>.Shared.Return(_utf8Buffer);
            }
            _utf8Buffer = ArrayPool<byte>.Shared.Rent(required);
        }
        int written = Encoding.UTF8.GetBytes(
            xml,
            0,
            xml.Length,
            _utf8Buffer,
            0);
        _stream.Write(_utf8Buffer, 0, written);
    }

    internal void Complete() {
        if (_completed) {
            return;
        }
        _stream.Write(Footer, 0, Footer.Length);
        _completed = true;
    }

    public void Dispose() {
        if (!_completed) {
            Complete();
        }
        if (_utf8Buffer != null) {
            ArrayPool<byte>.Shared.Return(_utf8Buffer);
            _utf8Buffer = null;
        }
    }
}
