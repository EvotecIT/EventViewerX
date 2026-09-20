using EventViewerX;

internal static class FidelityComparer {
    internal static Fidelity Compare(EventObject[] expected, EventObject[] actual) {
        Dictionary<long, EventObject> expectedById = expected
            .Where(static item => item.RecordId.HasValue)
            .GroupBy(static item => item.RecordId!.Value)
            .ToDictionary(static group => group.Key, static group => group.First());
        int compared = 0;
        int identityMatches = 0;
        int eventIdMatches = 0;
        int providerMatches = 0;
        int channelMatches = 0;
        int computerMatches = 0;
        int timestampMatches = 0;
        int timestampExactMatches = 0;
        var matchedExpectedIds = new HashSet<long>();
        foreach (EventObject item in actual.Where(static item => item.RecordId.HasValue)) {
            long recordId = item.RecordId!.Value;
            if (!expectedById.TryGetValue(recordId, out EventObject? source) ||
                !matchedExpectedIds.Add(recordId)) {
                continue;
            }
            compared++;
            bool eventId = source.Id == item.Id;
            bool provider = string.Equals(source.ProviderName, item.ProviderName, StringComparison.Ordinal);
            bool channel = string.Equals(source.OriginalLogName, item.OriginalLogName, StringComparison.Ordinal);
            bool computer = string.Equals(source.SourceComputer, item.SourceComputer, StringComparison.OrdinalIgnoreCase);
            long timestampDifference = Math.Abs(
                source.TimeCreated.ToUniversalTime().Ticks - item.TimeCreated.ToUniversalTime().Ticks);
            bool timestampExact = timestampDifference == 0;
            bool timestamp = timestampDifference <= 10;
            eventIdMatches += eventId ? 1 : 0;
            providerMatches += provider ? 1 : 0;
            channelMatches += channel ? 1 : 0;
            computerMatches += computer ? 1 : 0;
            timestampMatches += timestamp ? 1 : 0;
            timestampExactMatches += timestampExact ? 1 : 0;
            if (eventId && provider && channel && computer && timestamp) {
                identityMatches++;
            }
        }
        int identityDenominator = Math.Max(expected.Length, actual.Length);
        return new Fidelity(
            expected.Length,
            actual.Length,
            compared,
            identityMatches,
            identityDenominator == 0 ? 1 : (double)identityMatches / identityDenominator,
            compared == 0 ? 0 : (double)eventIdMatches / compared,
            compared == 0 ? 0 : (double)providerMatches / compared,
            compared == 0 ? 0 : (double)channelMatches / compared,
            compared == 0 ? 0 : (double)computerMatches / compared,
            compared == 0 ? 0 : (double)timestampMatches / compared,
            compared == 0 ? 0 : (double)timestampExactMatches / compared,
            Math.Max(0, expected.Length - compared),
            Math.Max(0, actual.Length - compared));
    }
}

internal sealed record Fidelity(
    int WindowsRecords,
    int PortableRecords,
    int ComparedRecords,
    int IdentityMatches,
    double IdentityMatchRatio,
    double EventIdMatchRatio,
    double ProviderMatchRatio,
    double ChannelMatchRatio,
    double ComputerMatchRatio,
    double TimestampMatchRatio,
    double TimestampExactMatchRatio,
    int MissingPortableRecords,
    int ExtraPortableRecords);

internal sealed record FidelityAggregate(
    IReadOnlyList<Fidelity> Iterations,
    double MinimumIdentityMatchRatio,
    double MinimumExactTimestampMatchRatio,
    int MaximumMissingPortableRecords,
    int MaximumExtraPortableRecords) {

    internal static FidelityAggregate Create(IReadOnlyList<Fidelity> fidelities) {
        if (fidelities.Count == 0) {
            throw new ArgumentException("At least one fidelity result is required.", nameof(fidelities));
        }
        return new FidelityAggregate(
            fidelities,
            fidelities.Min(static item => item.IdentityMatchRatio),
            fidelities.Min(static item => item.TimestampExactMatchRatio),
            fidelities.Max(static item => item.MissingPortableRecords),
            fidelities.Max(static item => item.ExtraPortableRecords));
    }
}
