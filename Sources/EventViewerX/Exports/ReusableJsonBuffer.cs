using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EventViewerX.Exports;

internal sealed class ReusableJsonBuffer : IDisposable {
    private readonly List<string> _dataKeys = new();
    private readonly MemoryStream _stream = new(4096);
    private readonly Utf8JsonWriter _writer;

    internal ReusableJsonBuffer() {
        _writer = new Utf8JsonWriter(_stream);
    }

    internal string SerializeProperties(IReadOnlyList<EventPropertyValue> properties) {
        Reset();
        EventJsonFields.WriteProperties(_writer, properties);
        return Finish();
    }

    internal string SerializeStrings(IEnumerable<string> values) {
        Reset();
        EventJsonFields.WriteStrings(_writer, values);
        return Finish();
    }

    internal string SerializeData(IReadOnlyDictionary<string, string> data) {
        Reset();
        EventJsonFields.WriteData(_writer, data, _dataKeys);
        return Finish();
    }

    internal string SerializeAttachments(IReadOnlyList<byte[]> attachments) {
        Reset();
        EventJsonFields.WriteAttachments(_writer, attachments);
        return Finish();
    }

    private void Reset() {
        _stream.Position = 0;
        _stream.SetLength(0);
        _writer.Reset(_stream);
    }

    private string Finish() {
        _writer.Flush();
        return Encoding.UTF8.GetString(
            _stream.GetBuffer(),
            0,
            checked((int)_stream.Length));
    }

    public void Dispose() {
        _writer.Dispose();
        _stream.Dispose();
    }
}
