using System.Text;

namespace EventViewerX.Evtx;

/// <summary>Frames compact evtx_dump XML output without assuming event payloads are single-line.</summary>
internal sealed class EvtxDumpXmlRecordFramer {
    internal const int MaximumRecordCharacters = 16 * 1024 * 1024;

    private StringBuilder? _record;

    internal bool TryAdd(string line, out string? xml) {
        xml = null;
        int first = FirstNonWhitespace(line);
        int last = LastNonWhitespace(line);
        if (_record == null) {
            if (first > last || StartsWith(line, first, "<?xml")) {
                return false;
            }
            if (!StartsWith(line, first, "<Event")) {
                throw new InvalidDataException(
                    "evtx_dump produced unexpected output before an Event XML fragment.");
            }
            if (line.Length > MaximumRecordCharacters) {
                throw new InvalidDataException(
                    $"evtx_dump produced an Event XML fragment larger than {MaximumRecordCharacters} characters.");
            }
            if (EndsWith(line, last, "</Event>")) {
                xml = line;
                return true;
            }
            int capacity = line.Length >= MaximumRecordCharacters - 256
                ? MaximumRecordCharacters
                : line.Length + 256;
            _record = new StringBuilder(capacity);
        } else {
            _record.Append('\n');
        }

        if (_record.Length > MaximumRecordCharacters - line.Length) {
            _record = null;
            throw new InvalidDataException(
                $"evtx_dump produced an Event XML fragment larger than {MaximumRecordCharacters} characters.");
        }
        _record.Append(line);
        if (!EndsWith(line, last, "</Event>")) {
            return false;
        }

        xml = _record.ToString();
        _record = null;
        return true;
    }

    internal void Complete() {
        if (_record != null) {
            _record = null;
            throw new InvalidDataException("evtx_dump ended inside an Event XML fragment.");
        }
    }

    private static int FirstNonWhitespace(string value) {
        int index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index])) {
            index++;
        }
        return index;
    }

    private static int LastNonWhitespace(string value) {
        int index = value.Length - 1;
        while (index >= 0 && char.IsWhiteSpace(value[index])) {
            index--;
        }
        return index;
    }

    private static bool StartsWith(string value, int first, string expected) =>
        first <= value.Length - expected.Length &&
        string.Compare(value, first, expected, 0, expected.Length, StringComparison.Ordinal) == 0;

    private static bool EndsWith(string value, int last, string expected) {
        int first = last - expected.Length + 1;
        return first >= 0 &&
               string.Compare(value, first, expected, 0, expected.Length, StringComparison.Ordinal) == 0;
    }
}
