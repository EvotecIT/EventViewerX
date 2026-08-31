namespace EventViewerX.Reporting;

/// <summary>Applies the shared deterministic normalizer registry to report rows.</summary>
public static class EventValueNormalizationEngine {
    /// <summary>Returns normalized metadata for every projected domain value without changing raw values.</summary>
    public static IReadOnlyDictionary<string, EventNormalizedValue> Normalize(EventReportRow row) {
        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }
        var result = new Dictionary<string, EventNormalizedValue>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> item in row.Values) {
            result[item.Key] = EventValueNormalizerRegistry.Normalize(new EventValueContext {
                ProviderName = row.Provider,
                EventId = row.EventId,
                TypeName = row.Type,
                FieldName = item.Key,
                RawValue = item.Value,
                Values = row.Values
            });
        }
        return result;
    }

    internal static void Populate(EventReportRow row) {
        row.NormalizedValues = Normalize(row);
    }
}
