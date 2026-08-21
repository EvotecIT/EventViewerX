namespace EventViewerX.Native;

internal sealed class WindowsEventSnapshotProjector : IDisposable {
    private readonly EventReadMode _readMode;
    private readonly bool _includeBookmark;
    private readonly string _queriedMachine;
    private readonly string _containerLog;
    private readonly WindowsEventSystemRenderer _systemRenderer;
    private readonly WindowsEventMessageRenderer? _messageRenderer;
    private readonly WindowsEventPayloadRenderer? _payloadRenderer;
    private readonly WindowsEventXmlRenderer? _xmlRenderer;
    private readonly WindowsEventBookmarkRenderer? _bookmarkRenderer;

    internal WindowsEventSnapshotProjector(
        EventReadMode readMode,
        IntPtr session,
        string queriedMachine,
        string containerLog,
        int messageLocale,
        int fallbackMessageLocale = 0,
        bool includeBookmark = true) {

        _readMode = readMode;
        _includeBookmark = includeBookmark;
        _queriedMachine = queriedMachine;
        _containerLog = containerLog;
        WindowsEventSystemRenderer? systemRenderer = null;
        WindowsEventMessageRenderer? messageRenderer = null;
        WindowsEventPayloadRenderer? payloadRenderer = null;
        WindowsEventXmlRenderer? xmlRenderer = null;
        WindowsEventBookmarkRenderer? bookmarkRenderer = null;
        try {
            systemRenderer = new WindowsEventSystemRenderer();
            if (readMode == EventReadMode.Message ||
                readMode == EventReadMode.Full ||
                readMode == EventReadMode.StructuredDataAndMessage) {
                messageRenderer = new WindowsEventMessageRenderer(
                    session,
                    null,
                    messageLocale,
                    fallbackMessageLocale);
            }
            if (readMode == EventReadMode.StructuredData ||
                readMode == EventReadMode.Full ||
                readMode == EventReadMode.StructuredDataAndMessage) {
                payloadRenderer = new WindowsEventPayloadRenderer();
            }
            if (includeBookmark &&
                (readMode == EventReadMode.Metadata ||
                 readMode == EventReadMode.RawXml)) {
                bookmarkRenderer =
                    new WindowsEventBookmarkRenderer();
            }
            if (readMode == EventReadMode.RawXml) {
                xmlRenderer = new WindowsEventXmlRenderer();
            }

            _systemRenderer = systemRenderer;
            _messageRenderer = messageRenderer;
            _payloadRenderer = payloadRenderer;
            _xmlRenderer = xmlRenderer;
            _bookmarkRenderer = bookmarkRenderer;
        } catch {
            bookmarkRenderer?.Dispose();
            xmlRenderer?.Dispose();
            payloadRenderer?.Dispose();
            messageRenderer?.Dispose();
            systemRenderer?.Dispose();
            throw;
        }
    }

    internal EventObject Project(IntPtr eventHandle) {
        NativeEventMetadata metadata = _systemRenderer.Render(eventHandle);
        switch (_readMode) {
            case EventReadMode.Metadata:
                return new EventObject(
                    metadata,
                    _bookmarkRenderer?.Render(eventHandle),
                    _queriedMachine,
                    _containerLog);
            case EventReadMode.Message:
                return new EventObject(
                    _messageRenderer!.Render(
                        eventHandle,
                        metadata,
                        _includeBookmark),
                    _queriedMachine,
                    _containerLog);
            case EventReadMode.StructuredData:
                return new EventObject(
                    _payloadRenderer!.Render(
                        eventHandle,
                        metadata,
                        _includeBookmark),
                    _queriedMachine,
                    _containerLog);
            case EventReadMode.RawXml:
                return new EventObject(
                    metadata,
                    _xmlRenderer!.Render(eventHandle),
                    _bookmarkRenderer?.Render(eventHandle),
                    _queriedMachine,
                    _containerLog);
            case EventReadMode.Full:
            case EventReadMode.StructuredDataAndMessage:
                NativeEventMessage message =
                    _messageRenderer!.Render(
                        eventHandle,
                        metadata,
                        includeBookmark:
                            _includeBookmark);
                NativeEventStructured structured =
                    _payloadRenderer!.Render(
                        eventHandle,
                        metadata,
                        _includeBookmark);
                return new EventObject(
                    new NativeEventFull(message, structured),
                    _queriedMachine,
                    _containerLog,
                    _readMode);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(_readMode),
                    _readMode,
                    "Unsupported event read mode.");
        }
    }

    public void Dispose() {
        _bookmarkRenderer?.Dispose();
        _xmlRenderer?.Dispose();
        _payloadRenderer?.Dispose();
        _messageRenderer?.Dispose();
        _systemRenderer.Dispose();
    }
}
