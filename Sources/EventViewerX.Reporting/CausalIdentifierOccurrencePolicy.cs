namespace EventViewerX.Reporting;

internal sealed class CausalIdentifierOccurrencePolicy : IMultiIdentityEventOccurrencePolicy {
    private static readonly string[] CausalFields = {
        "OperationCorrelationId",
        "ApplicationCorrelationId",
        "CorrelationId",
        "RelatedActivityId",
        "ActivityId",
        "ActivityID",
        "TransactionId",
        "BatchId"
    };

    public string Name => "causal-identifier";

    public int Version => 6;

    public bool TryGetIdentity(EventReportRow observation, out string identity, out string reason) {
        EventOccurrencePolicyIdentity? first = GetIdentities(observation).FirstOrDefault();
        if (first != null) {
            identity = first.Identity;
            reason = first.Reason;
            return true;
        }
        identity = string.Empty;
        reason = string.Empty;
        return false;
    }

    public IReadOnlyList<EventOccurrencePolicyIdentity> GetIdentities(EventReportRow observation) {
        var identities = new List<EventOccurrencePolicyIdentity>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        if (observation.RelatedActivityId is Guid relatedActivityId && relatedActivityId != Guid.Empty) {
            AddIdentity(observation, "ActivityId", relatedActivityId.ToString("D"), identities, emitted);
        }
        if (observation.ActivityId is Guid activityId && activityId != Guid.Empty) {
            AddIdentity(observation, "ActivityId", activityId.ToString("D"), identities, emitted);
        }
        foreach (string field in CausalFields) {
            if (!TryGetPayloadValue(observation, field, out object? raw)) {
                continue;
            }
            string value = raw is System.Collections.IEnumerable and not string
                ? EventAggregationEngine.Canonicalize(raw)
                : Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (value.Length == 0 || value == "-" || value == Guid.Empty.ToString()) {
                continue;
            }
            AddIdentity(
                observation,
                string.Equals(field, "RelatedActivityId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "ActivityID", StringComparison.OrdinalIgnoreCase)
                    ? "ActivityId"
                    : field,
                value,
                identities,
                emitted);
        }
        return identities;
    }

    private static bool TryGetPayloadValue(
        EventReportRow observation,
        string field,
        out object? value) {

        if (observation.NormalizedValues.TryGetValue(field, out EventNormalizedValue? normalized)) {
            value = normalized.Value;
            return true;
        }
        if (observation.Values.TryGetValue(field, out value)) {
            return true;
        }
        KeyValuePair<string, object?> matched = observation.Values.FirstOrDefault(item =>
            string.Equals(item.Key, field, StringComparison.OrdinalIgnoreCase));
        value = matched.Value;
        return matched.Key != null;
    }

    private static void AddIdentity(
        EventReportRow observation,
        string field,
        string value,
        ICollection<EventOccurrencePolicyIdentity> identities,
        ISet<string> emitted) {

        string producer = string.IsNullOrWhiteSpace(observation.Provider)
            ? observation.Type
            : observation.Provider;
        string identity = string.Join(
            "\0",
            producer.Trim().ToUpperInvariant(),
            observation.SourceComputer.Trim().ToUpperInvariant(),
            field.ToUpperInvariant(),
            value.ToUpperInvariant());
        if (emitted.Add(identity)) {
            identities.Add(new EventOccurrencePolicyIdentity(
                identity,
                $"Shared {field} value '{value}' from '{producer}'."));
        }
    }
}
