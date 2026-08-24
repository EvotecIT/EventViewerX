namespace EventViewerX.Reporting;

internal sealed class CausalIdentifierOccurrencePolicy : IEventOccurrencePolicy {
    private static readonly string[] CausalFields = {
        "OperationCorrelationId",
        "ApplicationCorrelationId",
        "CorrelationId",
        "ActivityId",
        "ActivityID",
        "RelatedActivityId",
        "TransactionId",
        "BatchId"
    };

    public string Name => "causal-identifier";

    public int Version => 3;

    public bool TryGetIdentity(EventReportRow observation, out string identity, out string reason) {
        if (observation.ActivityId is Guid activityId && activityId != Guid.Empty) {
            return CreateIdentity(observation, "ActivityId", activityId.ToString("D"), out identity, out reason);
        }
        if (observation.RelatedActivityId is Guid relatedActivityId && relatedActivityId != Guid.Empty) {
            return CreateIdentity(observation, "ActivityId", relatedActivityId.ToString("D"), out identity, out reason);
        }
        IReadOnlyDictionary<string, object?> values = observation.ToNormalizedDictionary();
        foreach (string field in CausalFields) {
            if (!values.TryGetValue(field, out object? raw)) {
                continue;
            }
            string value = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (value.Length == 0 || value == "-" || value == Guid.Empty.ToString()) {
                continue;
            }
            return CreateIdentity(
                observation,
                string.Equals(field, "RelatedActivityId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "ActivityID", StringComparison.OrdinalIgnoreCase)
                    ? "ActivityId"
                    : field,
                value,
                out identity,
                out reason);
        }
        identity = string.Empty;
        reason = string.Empty;
        return false;
    }

    private static bool CreateIdentity(
        EventReportRow observation,
        string field,
        string value,
        out string identity,
        out string reason) {

        string producer = string.IsNullOrWhiteSpace(observation.Provider)
            ? observation.Type
            : observation.Provider;
        identity = string.Join(
            "\0",
            producer.Trim().ToUpperInvariant(),
            observation.SourceComputer.Trim().ToUpperInvariant(),
            field.ToUpperInvariant(),
            value.ToUpperInvariant());
        reason = $"Shared {field} value '{value}' from '{producer}'.";
        return true;
    }
}
