using System.Text;

namespace EventViewerX.Evtx;

/// <summary>
/// Frames compact evtx_dump XML output without assuming event payloads are single-line.
/// Per-record XML declarations provide an unambiguous recovery boundary after a truncated fragment.
/// </summary>
internal sealed class EvtxDumpXmlRecordFramer {
    internal const int MaximumRecordCharacters = 16 * 1024 * 1024;

    private StringBuilder? _record;
    private readonly EvtxDumpXmlDepthTracker _depthTracker = new();

    internal bool TryAdd(string line, out string? xml) =>
        TryAdd(line, out xml, out _);

    internal bool TryAdd(string line, out string? xml, out string? recoveryError) {
        try {
            return TryAddCore(line, out xml, out recoveryError);
        } catch {
            Reset();
            throw;
        }
    }

    private bool TryAddCore(string line, out string? xml, out string? recoveryError) {
        xml = null;
        recoveryError = null;
        int first = FirstNonWhitespace(line);
        if (_depthTracker.CanResynchronizeAtDocumentBoundary &&
            TryGetXmlDeclarationEnd(line, first, out int declarationEnd)) {
            if (_record != null) {
                recoveryError = "evtx_dump started a new XML document before the previous Event fragment ended.";
                Reset();
            }
            first = FirstNonWhitespace(line, declarationEnd);
            if (first == line.Length) {
                return false;
            }
        }
        if (_record == null) {
            if (first == line.Length) {
                return false;
            }
            if (!StartsWithEventRoot(line, first)) {
                throw new InvalidDataException(
                    "evtx_dump produced unexpected output before an Event XML fragment.");
            }
            if (line.Length > MaximumRecordCharacters) {
                throw new InvalidDataException(
                    $"evtx_dump produced an Event XML fragment larger than {MaximumRecordCharacters} characters.");
            }
            if (_depthTracker.Process(line)) {
                xml = line;
                Reset();
                return true;
            }
            int capacity = line.Length >= MaximumRecordCharacters - 256
                ? MaximumRecordCharacters
                : line.Length + 256;
            _record = new StringBuilder(capacity);
            _record.Append(line);
            return false;
        }

        _record.Append('\n');
        if (_record.Length > MaximumRecordCharacters - line.Length) {
            Reset();
            throw new InvalidDataException(
                $"evtx_dump produced an Event XML fragment larger than {MaximumRecordCharacters} characters.");
        }
        _record.Append(line);
        if (!_depthTracker.Process(line)) {
            return false;
        }

        xml = _record.ToString();
        Reset();
        return true;
    }

    internal void Complete() {
        if (_record != null) {
            Reset();
            throw new InvalidDataException("evtx_dump ended inside an Event XML fragment.");
        }
    }

    private void Reset() {
        _record = null;
        _depthTracker.Reset();
    }

    private static int FirstNonWhitespace(string value) {
        return FirstNonWhitespace(value, 0);
    }

    private static int FirstNonWhitespace(string value, int start) {
        int index = start;
        while (index < value.Length && char.IsWhiteSpace(value[index])) {
            index++;
        }
        return index;
    }

    private static bool StartsWith(string value, int first, string expected) =>
        first <= value.Length - expected.Length &&
        string.Compare(value, first, expected, 0, expected.Length, StringComparison.Ordinal) == 0;

    private static bool StartsWithEventRoot(string value, int first) {
        const string eventStart = "<Event";
        if (!StartsWith(value, first, eventStart)) {
            return false;
        }
        int boundary = first + eventStart.Length;
        return boundary == value.Length ||
               char.IsWhiteSpace(value[boundary]) ||
               value[boundary] is '>' or '/';
    }

    private static bool TryGetXmlDeclarationEnd(string value, int first, out int end) {
        end = first;
        const string declarationStart = "<?xml";
        if (!StartsWith(value, first, declarationStart)) {
            return false;
        }
        int boundary = first + declarationStart.Length;
        if (boundary < value.Length &&
            !char.IsWhiteSpace(value[boundary]) &&
            value[boundary] != '?') {
            return false;
        }
        int terminator = value.IndexOf("?>", boundary, StringComparison.Ordinal);
        if (terminator < 0) {
            throw new InvalidDataException("evtx_dump produced an incomplete XML declaration.");
        }
        end = terminator + 2;
        return true;
    }

}
