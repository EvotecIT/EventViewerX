namespace EventViewerX;

internal static class EventReadModeValidation {
    internal static void EnsureDefined(
        EventReadMode readMode,
        string parameterName) {

        if (!Enum.IsDefined(
                typeof(EventReadMode),
                readMode)) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                readMode,
                "The event read mode is not supported.");
        }
    }
}
