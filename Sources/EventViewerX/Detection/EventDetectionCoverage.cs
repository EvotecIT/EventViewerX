using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>Expected and observed data-source coverage attached to every detection outcome.</summary>
public sealed class EventDetectionCoverage {
    /// <summary>Current durable JSON contract version.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly JsonSerializerOptions CompactJsonOptions =
        new(JsonOptions) { WriteIndented = false };
    private static readonly JsonSerializerOptions IndentedJsonOptions =
        new(JsonOptions) { WriteIndented = true };

    private EventDetectionCoverage(
        IReadOnlyList<string> expectedTargets,
        IReadOnlyList<string> observedTargets,
        IReadOnlyList<string> expectedChannels,
        IReadOnlyList<string> observedChannels,
        IReadOnlyList<string> expectedProviders,
        IReadOnlyList<string> observedProviders,
        IReadOnlyList<int> expectedEventIds,
        IReadOnlyList<int> observedEventIds,
        IReadOnlyList<EventType> expectedEventTypes,
        IReadOnlyList<EventType> observedEventTypes,
        IReadOnlyList<string> failures,
        bool declared) {

        ExpectedTargets = Array.AsReadOnly(expectedTargets.ToArray());
        ObservedTargets = Array.AsReadOnly(observedTargets.ToArray());
        ExpectedChannels = Array.AsReadOnly(expectedChannels.ToArray());
        ObservedChannels = Array.AsReadOnly(observedChannels.ToArray());
        ExpectedProviders = Array.AsReadOnly(expectedProviders.ToArray());
        ObservedProviders = Array.AsReadOnly(observedProviders.ToArray());
        ExpectedEventIds = Array.AsReadOnly(expectedEventIds.ToArray());
        ObservedEventIds = Array.AsReadOnly(observedEventIds.ToArray());
        ExpectedEventTypes = Array.AsReadOnly(expectedEventTypes.ToArray());
        ObservedEventTypes = Array.AsReadOnly(observedEventTypes.ToArray());
        Failures = Array.AsReadOnly(failures.ToArray());
        IsDeclared = declared;
        MissingTargets = Missing(ExpectedTargets, ObservedTargets);
        MissingChannels = Missing(ExpectedChannels, ObservedChannels);
        MissingProviders = Missing(ExpectedProviders, ObservedProviders);
        MissingEventIds = Array.AsReadOnly(ExpectedEventIds.Except(ObservedEventIds).ToArray());
        MissingEventTypes = Array.AsReadOnly(ExpectedEventTypes.Except(ObservedEventTypes).ToArray());
    }

    /// <summary>Creates explicit expected-versus-observed source coverage.</summary>
    public static EventDetectionCoverage Create(
        IEnumerable<string>? expectedTargets = null,
        IEnumerable<string>? observedTargets = null,
        IEnumerable<string>? expectedChannels = null,
        IEnumerable<string>? observedChannels = null,
        IEnumerable<string>? expectedProviders = null,
        IEnumerable<string>? observedProviders = null,
        IEnumerable<int>? expectedEventIds = null,
        IEnumerable<int>? observedEventIds = null,
        IEnumerable<EventType>? expectedEventTypes = null,
        IEnumerable<EventType>? observedEventTypes = null,
        IEnumerable<string>? failures = null) => new(
            Normalize(expectedTargets),
            Normalize(observedTargets),
            Normalize(expectedChannels),
            Normalize(observedChannels),
            Normalize(expectedProviders),
            Normalize(observedProviders),
            NormalizeEventIds(expectedEventIds),
            NormalizeEventIds(observedEventIds),
            (expectedEventTypes ?? Array.Empty<EventType>()).Distinct().ToArray(),
            (observedEventTypes ?? Array.Empty<EventType>()).Distinct().ToArray(),
            Normalize(failures),
            declared: true);

    /// <summary>Whether a caller explicitly declared the expected collection scope.</summary>
    public bool IsDeclared { get; }
    /// <summary>Targets expected in the evaluated window.</summary>
    public IReadOnlyList<string> ExpectedTargets { get; }
    /// <summary>Targets successfully represented by collection evidence.</summary>
    public IReadOnlyList<string> ObservedTargets { get; }
    /// <summary>Channels expected in the evaluated window.</summary>
    public IReadOnlyList<string> ExpectedChannels { get; }
    /// <summary>Channels successfully represented by collection evidence.</summary>
    public IReadOnlyList<string> ObservedChannels { get; }
    /// <summary>Providers expected in the evaluated window.</summary>
    public IReadOnlyList<string> ExpectedProviders { get; }
    /// <summary>Providers successfully represented by collection evidence.</summary>
    public IReadOnlyList<string> ObservedProviders { get; }
    /// <summary>Native event IDs expected from the selected query contract.</summary>
    public IReadOnlyList<int> ExpectedEventIds { get; }
    /// <summary>Native event-ID scopes successfully covered by the query contract.</summary>
    public IReadOnlyList<int> ObservedEventIds { get; }
    /// <summary>Typed projections expected from the selected query contract.</summary>
    public IReadOnlyList<EventType> ExpectedEventTypes { get; }
    /// <summary>Typed projection scopes successfully covered by the query contract.</summary>
    public IReadOnlyList<EventType> ObservedEventTypes { get; }
    /// <summary>Expected targets without successful coverage.</summary>
    public IReadOnlyList<string> MissingTargets { get; }
    /// <summary>Expected channels without successful coverage.</summary>
    public IReadOnlyList<string> MissingChannels { get; }
    /// <summary>Expected providers without successful coverage.</summary>
    public IReadOnlyList<string> MissingProviders { get; }
    /// <summary>Expected native event-ID scopes without successful coverage.</summary>
    public IReadOnlyList<int> MissingEventIds { get; }
    /// <summary>Expected typed scopes without successful coverage.</summary>
    public IReadOnlyList<EventType> MissingEventTypes { get; }
    /// <summary>Source, query, or collection failures affecting the window.</summary>
    public IReadOnlyList<string> Failures { get; }
    /// <summary>Whether the declared expected scope was completely collected.</summary>
    public bool IsComplete => IsDeclared &&
        MissingTargets.Count == 0 &&
        MissingChannels.Count == 0 &&
        MissingProviders.Count == 0 &&
        MissingEventIds.Count == 0 &&
        MissingEventTypes.Count == 0 &&
        Failures.Count == 0;

    /// <summary>Serializes the immutable coverage snapshot using the versioned public contract.</summary>
    public string ToJson(bool indented = false) => JsonSerializer.Serialize(
        new CoverageEnvelope {
            SchemaVersion = CurrentSchemaVersion,
            IsDeclared = IsDeclared,
            ExpectedTargets = ExpectedTargets.ToArray(),
            ObservedTargets = ObservedTargets.ToArray(),
            ExpectedChannels = ExpectedChannels.ToArray(),
            ObservedChannels = ObservedChannels.ToArray(),
            ExpectedProviders = ExpectedProviders.ToArray(),
            ObservedProviders = ObservedProviders.ToArray(),
            ExpectedEventIds = ExpectedEventIds.ToArray(),
            ObservedEventIds = ObservedEventIds.ToArray(),
            ExpectedEventTypes = ExpectedEventTypes.ToArray(),
            ObservedEventTypes = ObservedEventTypes.ToArray(),
            Failures = Failures.ToArray()
        },
        indented ? IndentedJsonOptions : CompactJsonOptions);

    /// <summary>Restores a versioned immutable coverage snapshot.</summary>
    public static EventDetectionCoverage FromJson(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new ArgumentException("Coverage JSON cannot be empty.", nameof(json));
        }
        CoverageEnvelope envelope = JsonSerializer.Deserialize<CoverageEnvelope>(json, JsonOptions) ??
            throw new InvalidDataException("Coverage JSON did not contain a document.");
        if (envelope.SchemaVersion != CurrentSchemaVersion) {
            throw new InvalidDataException(
                $"Coverage schema version '{envelope.SchemaVersion}' is not supported by this EventViewerX build.");
        }
        EventType[] expectedTypes = ValidateEventTypes(envelope.ExpectedEventTypes, nameof(envelope.ExpectedEventTypes));
        EventType[] observedTypes = ValidateEventTypes(envelope.ObservedEventTypes, nameof(envelope.ObservedEventTypes));
        return new EventDetectionCoverage(
            Normalize(envelope.ExpectedTargets),
            Normalize(envelope.ObservedTargets),
            Normalize(envelope.ExpectedChannels),
            Normalize(envelope.ObservedChannels),
            Normalize(envelope.ExpectedProviders),
            Normalize(envelope.ObservedProviders),
            NormalizeEventIds(envelope.ExpectedEventIds),
            NormalizeEventIds(envelope.ObservedEventIds),
            expectedTypes,
            observedTypes,
            Normalize(envelope.Failures),
            envelope.IsDeclared);
    }

    internal static EventDetectionCoverage Unknown() => new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<int>(),
        Array.Empty<int>(),
        Array.Empty<EventType>(),
        Array.Empty<EventType>(),
        new[] { "The caller did not declare expected data-source coverage." },
        declared: false);

    internal EventDetectionCoverage Snapshot() => new(
        ExpectedTargets,
        ObservedTargets,
        ExpectedChannels,
        ObservedChannels,
        ExpectedProviders,
        ObservedProviders,
        ExpectedEventIds,
        ObservedEventIds,
        ExpectedEventTypes,
        ObservedEventTypes,
        Failures,
        IsDeclared);

    /// <summary>Returns a detached coverage snapshot with additional source or collection failures.</summary>
    public EventDetectionCoverage WithFailures(IEnumerable<string> failures) => new(
        ExpectedTargets,
        ObservedTargets,
        ExpectedChannels,
        ObservedChannels,
        ExpectedProviders,
        ObservedProviders,
        ExpectedEventIds,
        ObservedEventIds,
        ExpectedEventTypes,
        ObservedEventTypes,
        Normalize(Failures.Concat(failures ?? Array.Empty<string>())),
        IsDeclared);

    private static string[] Normalize(IEnumerable<string>? values) =>
        (values ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> Missing(
        IEnumerable<string> expected,
        IEnumerable<string> observed) {

        var available = new HashSet<string>(observed, StringComparer.OrdinalIgnoreCase);
        return Array.AsReadOnly(expected.Where(item => !available.Contains(item)).ToArray());
    }

    private static int[] NormalizeEventIds(IEnumerable<int>? values) =>
        (values ?? Array.Empty<int>()).Where(static value => value >= 0).Distinct().OrderBy(static value => value).ToArray();

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static EventType[] ValidateEventTypes(IEnumerable<EventType>? values, string field) {
        EventType[] snapshot = (values ?? Array.Empty<EventType>()).Distinct().ToArray();
        EventType[] invalid = snapshot
            .Where(value => !Enum.IsDefined(typeof(EventType), value))
            .ToArray();
        if (invalid.Length > 0) {
            throw new InvalidDataException($"Coverage field '{field}' contains unsupported event type '{invalid[0]}'.");
        }
        return snapshot;
    }

    private sealed class CoverageEnvelope {
        public int SchemaVersion { get; set; }
        public bool IsDeclared { get; set; }
        public string[] ExpectedTargets { get; set; } = Array.Empty<string>();
        public string[] ObservedTargets { get; set; } = Array.Empty<string>();
        public string[] ExpectedChannels { get; set; } = Array.Empty<string>();
        public string[] ObservedChannels { get; set; } = Array.Empty<string>();
        public string[] ExpectedProviders { get; set; } = Array.Empty<string>();
        public string[] ObservedProviders { get; set; } = Array.Empty<string>();
        public int[] ExpectedEventIds { get; set; } = Array.Empty<int>();
        public int[] ObservedEventIds { get; set; } = Array.Empty<int>();
        public EventType[] ExpectedEventTypes { get; set; } = Array.Empty<EventType>();
        public EventType[] ObservedEventTypes { get; set; } = Array.Empty<EventType>();
        public string[] Failures { get; set; } = Array.Empty<string>();
    }
}
