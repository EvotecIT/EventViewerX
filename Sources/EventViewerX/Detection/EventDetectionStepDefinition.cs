namespace EventViewerX;

/// <summary>One independently selectable step in a temporal detection rule.</summary>
public sealed class EventDetectionStepDefinition {
    /// <summary>Stable step name used by explanations and fixture diagnostics.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Optional typed event selectors.</summary>
    public IReadOnlyList<EventType> EventTypes { get; set; } = Array.Empty<EventType>();
    /// <summary>Optional native event-ID selectors.</summary>
    public IReadOnlyList<int> EventIds { get; set; } = Array.Empty<int>();
    /// <summary>Optional original channel selectors.</summary>
    public IReadOnlyList<string> Channels { get; set; } = Array.Empty<string>();
    /// <summary>Optional provider selectors.</summary>
    public IReadOnlyList<string> Providers { get; set; } = Array.Empty<string>();
    /// <summary>Optional semantic field predicate.</summary>
    public EventPredicate? Predicate { get; set; }

    internal EventDetectionStepDefinition Snapshot(int index) {
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 200) {
            throw new InvalidDataException($"Steps[{index}].Name is required and cannot exceed 200 characters.");
        }
        EventType[] eventTypes = (EventTypes ?? Array.Empty<EventType>()).Distinct().ToArray();
        int[] eventIds = (EventIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (eventIds.Any(static id => id <= 0)) {
            throw new InvalidDataException($"Steps[{index}].EventIds must contain positive values.");
        }
        string[] channels = NormalizeText(Channels, index, nameof(Channels));
        string[] providers = NormalizeText(Providers, index, nameof(Providers));
        Predicate?.Validate();
        return new EventDetectionStepDefinition {
            Name = name,
            EventTypes = eventTypes,
            EventIds = eventIds,
            Channels = channels,
            Providers = providers,
            Predicate = Predicate?.Clone()
        };
    }

    private static string[] NormalizeText(IReadOnlyList<string>? values, int index, string property) {
        string[] normalized = (values ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Any(static value => value.Length > 2048)) {
            throw new InvalidDataException($"Steps[{index}].{property} cannot contain values longer than 2048 characters.");
        }
        return normalized;
    }
}
