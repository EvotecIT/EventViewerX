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

    public int Version => 7;

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
            string value = CanonicalizeValue(raw);
            if (value.Length == 0 || value == "-") {
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

    private static string CanonicalizeValue(object? raw) {
        if (raw is System.Collections.IEnumerable values and not string) {
            var normalized = new List<object?>();
            foreach (object? item in values) {
                if (TryCanonicalizeGuid(item, out string canonicalGuid)) {
                    if (canonicalGuid.Length > 0) {
                        normalized.Add(canonicalGuid);
                    }
                } else {
                    normalized.Add(item);
                }
            }
            return normalized.Count switch {
                0 => string.Empty,
                1 => CanonicalizeValue(normalized[0]),
                _ => EventAggregationEngine.Canonicalize(normalized)
            };
        }
        string value = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return TryCanonicalizeGuid(value, out string canonical) ? canonical : value;
    }

    private static bool TryCanonicalizeGuid(object? value, out string canonical) {
        string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (Guid.TryParse(text, out Guid guid)) {
            canonical = guid == Guid.Empty ? string.Empty : guid.ToString("D");
            return true;
        }
        canonical = string.Empty;
        return false;
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
