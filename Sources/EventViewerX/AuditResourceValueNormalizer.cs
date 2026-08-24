namespace EventViewerX;

internal sealed class AuditResourceValueNormalizer : IEventValueNormalizer {
    private static readonly IReadOnlyDictionary<string, string> KnownValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["%%14674"] = "Value Added",
            ["%%14675"] = "Value Deleted",
            ["%%14676"] = "Unknown"
        };

    public string Name => "windows-audit-resource";

    public int Version => 1;

    public bool CanNormalize(EventValueContext context) =>
        EventValueNormalizer.Format(context.RawValue).Trim().StartsWith("%%", StringComparison.Ordinal);

    public EventNormalizedValue Normalize(EventValueContext context) {
        string raw = EventValueNormalizer.Format(context.RawValue).Trim();
        return KnownValues.TryGetValue(raw, out string? display)
            ? EventValueNormalizer.Create(
                context,
                display,
                display,
                EventNormalizedValueKind.ResourceIdentifier,
                Name,
                Version)
            : EventValueNormalizer.Create(
                context,
                context.RawValue,
                raw,
                EventNormalizedValueKind.ResourceIdentifier,
                Name,
                Version,
                EventNormalizationOutcome.UnknownValue,
                warnings: new[] { $"Windows resource identifier '{raw}' is not in the built-in deterministic catalog." });
    }
}
