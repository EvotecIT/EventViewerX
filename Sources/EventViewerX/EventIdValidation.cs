namespace EventViewerX;

internal static class EventIdValidation {
    internal const int Minimum = 0;
    internal const int Maximum = ushort.MaxValue;

    internal static List<int> Normalize(
        IEnumerable<int> eventIds,
        string parameterName) {

        if (eventIds == null) {
            throw new ArgumentNullException(parameterName);
        }

        var normalized = new HashSet<int>();
        foreach (int eventId in eventIds) {
            if (eventId < Minimum ||
                eventId > Maximum) {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    eventId,
                    $"Event IDs must be between {Minimum} and {Maximum}.");
            }
            normalized.Add(eventId);
        }
        return normalized
            .OrderBy(static eventId => eventId)
            .ToList();
    }
}
