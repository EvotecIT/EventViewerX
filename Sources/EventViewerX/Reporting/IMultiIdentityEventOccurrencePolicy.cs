namespace EventViewerX.Reporting;

internal interface IMultiIdentityEventOccurrencePolicy : IEventOccurrencePolicy {
    IReadOnlyList<EventOccurrencePolicyIdentity> GetIdentities(EventReportRow observation);
}

internal sealed class EventOccurrencePolicyIdentity {
    internal EventOccurrencePolicyIdentity(string identity, string reason) {
        Identity = identity;
        Reason = reason;
    }

    internal string Identity { get; }
    internal string Reason { get; }
}
