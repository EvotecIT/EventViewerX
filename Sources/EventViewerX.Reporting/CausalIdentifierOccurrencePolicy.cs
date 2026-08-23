namespace EventViewerX.Reporting;

internal sealed class CausalIdentifierOccurrencePolicy : IEventOccurrencePolicy {
    private static readonly string[] CausalFields = {
        "OperationCorrelationId",
        "ApplicationCorrelationId",
        "CorrelationId",
        "ActivityId",
        "ActivityID",
        "TransactionId",
        "BatchId"
    };

    public string Name => "causal-identifier";

    public int Version => 1;

    public bool TryGetIdentity(EventReportRow observation, out string identity, out string reason) {
        IReadOnlyDictionary<string, object?> values = observation.ToNormalizedDictionary();
        foreach (string field in CausalFields) {
            if (!values.TryGetValue(field, out object? raw)) {
                continue;
            }
            string value = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (value.Length == 0 || value == "-" || value == Guid.Empty.ToString()) {
                continue;
            }
            identity = string.Join("\0", observation.Type.ToUpperInvariant(), observation.EventId, field.ToUpperInvariant(), value.ToUpperInvariant());
            reason = $"Shared {field} value '{value}'.";
            return true;
        }
        identity = string.Empty;
        reason = string.Empty;
        return false;
    }
}
