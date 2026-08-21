namespace EventViewerX.Native;

internal readonly struct NativeEventFull {
    internal NativeEventFull(
        NativeEventMessage message,
        NativeEventStructured structured) {

        Message = message;
        Structured = structured;
    }

    internal NativeEventMessage Message { get; }
    internal NativeEventStructured Structured { get; }
}
