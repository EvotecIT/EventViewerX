namespace EventViewerX;

internal sealed class DirectoryOperationValueNormalizer : IEventValueNormalizer {
    private static readonly IReadOnlyDictionary<string, string> Operations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["%%14674"] = "Value Added",
            ["%%14675"] = "Value Deleted",
            ["%%14676"] = "Unknown"
        };

    public string Name => "directory-operation";

    public int Version => 1;

    public bool CanNormalize(EventValueContext context) =>
        string.Equals(context.FieldName, "OperationType", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(context.FieldName, "ActionDetail", StringComparison.OrdinalIgnoreCase);

    public EventNormalizedValue Normalize(EventValueContext context) {
        string formatted = EventValueNormalizer.Format(context.RawValue);
        string raw = formatted.Trim();
        if (Operations.TryGetValue(raw, out string? value)) {
            return EventValueNormalizer.Create(
                context,
                value,
                value,
                EventNormalizedValueKind.DirectoryOperation,
                Name,
                Version);
        }
        if (Operations.Values.Contains(raw, StringComparer.OrdinalIgnoreCase)) {
            string canonical = Operations.Values.First(value => string.Equals(
                value,
                raw,
                StringComparison.OrdinalIgnoreCase));
            return EventValueNormalizer.Create(
                context,
                canonical,
                canonical,
                EventNormalizedValueKind.DirectoryOperation,
                Name,
                Version,
                string.Equals(formatted, canonical, StringComparison.Ordinal)
                    ? EventNormalizationOutcome.Unchanged
                    : EventNormalizationOutcome.Normalized);
        }
        return EventValueNormalizer.Create(
            context,
            context.RawValue,
            raw,
            EventNormalizedValueKind.DirectoryOperation,
            Name,
            Version,
            EventNormalizationOutcome.UnknownValue,
            warnings: new[] { $"Unknown directory operation value '{raw}'." });
    }
}
