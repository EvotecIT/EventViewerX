using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace EventViewerX.Exports;

internal sealed class EventCsvWriter : IEventExportWriter {
    private static readonly char[] QuotingCharacters = { ',', '"', '\r', '\n' };
    private static readonly string[] Header = {
        "TimeCreated",
        "RecordId",
        "Id",
        "ProviderName",
        "MachineName",
        "LogName",
        "Level",
        "LevelDisplayName",
        "Task",
        "TaskDisplayName",
        "Opcode",
        "OpcodeDisplayName",
        "Keywords",
        "KeywordDisplayNames",
        "ProcessId",
        "ThreadId",
        "UserId",
        "MessageCulture",
        "MessageRenderStatus",
        "MessageRenderErrorCode",
        "Message",
        "Properties",
        "Data",
        "Attachments",
        "Xml"
    };

    private readonly StreamWriter _writer;
    private readonly ReusableJsonBuffer _json = new();

    internal EventCsvWriter(Stream stream) {
        _writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            1024 * 64,
            leaveOpen: true);
        _writer.WriteLine(string.Join(",", Header));
    }

    public void Write(EventObject eventObject) {
        WriteField(eventObject.TimeCreated == DateTime.MinValue
            ? string.Empty
            : eventObject.TimeCreated.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.RecordId?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.Id.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.ProviderName);
        WriteSeparator();
        WriteField(eventObject.MachineName);
        WriteSeparator();
        WriteField(eventObject.LogName);
        WriteSeparator();
        WriteField(eventObject.Level?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.LevelDisplayName);
        WriteSeparator();
        WriteField(eventObject.Task?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.TaskDisplayName);
        WriteSeparator();
        WriteField(eventObject.Opcode?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.OpcodeDisplayName);
        WriteSeparator();
        WriteField(eventObject.Keywords?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(_json.SerializeStrings(eventObject.KeywordsDisplayNames));
        WriteSeparator();
        WriteField(eventObject.ProcessId?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.ThreadId?.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.UserId?.Value);
        WriteSeparator();
        WriteField(eventObject.MessageCulture);
        WriteSeparator();
        WriteField(eventObject.MessageRenderStatus.ToString());
        WriteSeparator();
        WriteField(eventObject.MessageRenderErrorCode.ToString(CultureInfo.InvariantCulture));
        WriteSeparator();
        WriteField(eventObject.Message);
        WriteSeparator();
        WriteField(_json.SerializeProperties(eventObject.Properties));
        WriteSeparator();
        WriteField(_json.SerializeData(eventObject.Data));
        WriteSeparator();
        WriteField(_json.SerializeAttachments(eventObject.Attachments));
        WriteSeparator();
        WriteField(eventObject.XMLData);
        _writer.WriteLine();
    }

    private void WriteSeparator() {
        _writer.Write(',');
    }

    private void WriteField(string? value) {
        if (value == null || value.Length == 0) {
            return;
        }

        bool quote = value.IndexOfAny(QuotingCharacters) >= 0;
        if (!quote) {
            _writer.Write(value);
            return;
        }

        _writer.Write('"');
        foreach (char character in value) {
            if (character == '"') {
                _writer.Write("\"\"");
            } else {
                _writer.Write(character);
            }
        }
        _writer.Write('"');
    }

    public void Complete() {
        _writer.Flush();
    }

    public void Dispose() {
        _json.Dispose();
        _writer.Dispose();
    }
}
