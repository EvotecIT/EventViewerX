using System.Globalization;

namespace EventViewerX.Reporting;

/// <summary>Creates collision-safe normalized JSON rows while retaining generic provider fields.</summary>
public static class EventReportJsonProjection {
    /// <summary>Projects one report row with the optional section contract and normalization evidence.</summary>
    public static IReadOnlyDictionary<string, object?> Project(
        EventReportRow row,
        EventReportSection? section = null) {

        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }
        IReadOnlyDictionary<string, object?> values = section == null
            ? row.ToNormalizedDictionary()
            : row.ToNormalizedDictionary(section);
        var output = values.ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        if (row.NormalizedValues.Count == 0) {
            return output;
        }
        if (output.ContainsKey(EventDefinition.OutputMetadataFieldName)) {
            object? providerValue = row.Values.TryGetValue(
                EventDefinition.OutputMetadataFieldName,
                out object? rawProviderValue)
                ? rawProviderValue
                : output[EventDefinition.OutputMetadataFieldName];
            output.Remove(EventDefinition.OutputMetadataFieldName);
            output[AllocateField(output, EventDefinition.OutputMetadataFieldName + "_ProviderField")] =
                providerValue;
        }
        output[EventDefinition.OutputMetadataFieldName] = new Dictionary<string, object?> {
            ["Normalization"] = row.NormalizedValues
        };
        return output;
    }

    private static string AllocateField(
        IReadOnlyDictionary<string, object?> output,
        string preferredName) {

        string name = preferredName;
        int suffix = 2;
        while (output.ContainsKey(name)) {
            name = preferredName + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }
        return name;
    }
}
