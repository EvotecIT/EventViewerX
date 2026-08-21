namespace EventViewerX.Native;

/// <summary>
/// Identifies a timeout that happened before a bounded native operation started.
/// </summary>
internal sealed class BoundedNativeOperationAdmissionTimeoutException :
    TimeoutException {
    internal BoundedNativeOperationAdmissionTimeoutException(
        string message)
        : base(message) {
    }
}
