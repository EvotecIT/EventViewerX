namespace EventViewerX;

/// <summary>Serializable native rule definition compiled by <see cref="EventDetectionPlan"/>.</summary>
public sealed class EventDetectionRuleDefinition {
    private const int MaximumTemporalStepCount = 64;

    /// <summary>Stable rule identifier.</summary>
    public string RuleId { get; set; } = string.Empty;
    /// <summary>Semantic content version.</summary>
    public string Version { get; set; } = "1.0.0";
    /// <summary>Pack that supplied this rule.</summary>
    public string PackId { get; set; } = string.Empty;
    /// <summary>Version of the supplying pack.</summary>
    public string PackVersion { get; set; } = string.Empty;
    /// <summary>Native, Sigma, or another source format.</summary>
    public string SourceKind { get; set; } = "Native";
    /// <summary>Source rule identifier when different from RuleId.</summary>
    public string SourceId { get; set; } = string.Empty;
    /// <summary>Source maturity status such as stable, test, or experimental.</summary>
    public string SourceStatus { get; set; } = string.Empty;
    /// <summary>SHA-256 hash of the source content when available.</summary>
    public string SourceHash { get; set; } = string.Empty;
    /// <summary>License applying to this rule content.</summary>
    public string License { get; set; } = string.Empty;
    /// <summary>Short operator-facing title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Purpose and detection behavior.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Default finding severity.</summary>
    public EventDetectionSeverity Severity { get; set; } = EventDetectionSeverity.Medium;
    /// <summary>Confidence from zero through one hundred.</summary>
    public int Confidence { get; set; } = 50;
    /// <summary>Rule behavior.</summary>
    public EventDetectionRuleKind Kind { get; set; }
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
    /// <summary>Required count for a threshold rule.</summary>
    public int Threshold { get; set; } = 1;
    /// <summary>Bounded threshold window.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>Optional field used to partition threshold state.</summary>
    public string? GroupBy { get; set; }
    /// <summary>Field whose unique values are counted by a distinct-value rule.</summary>
    public string? DistinctBy { get; set; }
    /// <summary>Steps required by temporal and ordered-temporal rules.</summary>
    public IReadOnlyList<EventDetectionStepDefinition> Steps { get; set; } = Array.Empty<EventDetectionStepDefinition>();
    /// <summary>ATT&amp;CK, product, platform, or operational tags.</summary>
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    /// <summary>Expected benign explanations.</summary>
    public IReadOnlyList<string> FalsePositives { get; set; } = Array.Empty<string>();
    /// <summary>Optional source references.</summary>
    public IReadOnlyList<string> References { get; set; } = Array.Empty<string>();

    /// <summary>Validates identifiers, selectors, predicates, and safety bounds.</summary>
    public void Validate() {
        RuleId = NormalizeRequired(RuleId, nameof(RuleId), 200);
        Version = NormalizeRequired(Version, nameof(Version), 64);
        PackId = NormalizeOptional(PackId, nameof(PackId), 200);
        PackVersion = NormalizeOptional(PackVersion, nameof(PackVersion), 64);
        SourceKind = NormalizeOptional(SourceKind, nameof(SourceKind), 64);
        SourceId = NormalizeOptional(SourceId, nameof(SourceId), 200);
        SourceStatus = NormalizeOptional(SourceStatus, nameof(SourceStatus), 64);
        SourceHash = NormalizeOptional(SourceHash, nameof(SourceHash), 128);
        License = NormalizeOptional(License, nameof(License), 200);
        Title = NormalizeRequired(Title, nameof(Title), 300);
        Description = Description?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(typeof(EventDetectionSeverity), Severity)) {
            throw new InvalidDataException("Severity is not supported.");
        }
        if (!Enum.IsDefined(typeof(EventDetectionRuleKind), Kind)) {
            throw new InvalidDataException("Kind is not supported.");
        }
        if (Confidence < 0 || Confidence > 100) {
            throw new InvalidDataException("Confidence must be between zero and one hundred.");
        }
        EventTypes = (EventTypes ?? Array.Empty<EventType>()).Distinct().ToArray();
        if (EventTypes.Any(static type => !Enum.IsDefined(typeof(EventType), type))) {
            throw new InvalidDataException("EventTypes contains an unsupported EventType value.");
        }
        EventIds = (EventIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (EventIds.Any(static id => id <= 0)) {
            throw new InvalidDataException("EventIds must contain positive values.");
        }
        Channels = NormalizeText(Channels, nameof(Channels));
        Providers = NormalizeText(Providers, nameof(Providers));
        Tags = NormalizeText(Tags, nameof(Tags));
        FalsePositives = NormalizeText(FalsePositives, nameof(FalsePositives));
        References = NormalizeText(References, nameof(References));
        Predicate?.Validate();
        if (Kind == EventDetectionRuleKind.Stateless) {
            Threshold = 1;
        } else if (Kind is EventDetectionRuleKind.Threshold or EventDetectionRuleKind.DistinctValue) {
            if (Threshold < 2) {
                throw new InvalidDataException("Threshold and distinct-value rules require Threshold of at least two.");
            }
        } else {
            Threshold = 1;
        }
        if (Kind != EventDetectionRuleKind.Stateless &&
            (Window <= TimeSpan.Zero || Window > TimeSpan.FromDays(30))) {
            throw new InvalidDataException("Stateful rule Window must be greater than zero and no longer than 30 days.");
        }
        string? groupBy = GroupBy;
        GroupBy = string.IsNullOrWhiteSpace(groupBy) ? null : groupBy!.Trim();
        string? distinctBy = DistinctBy;
        DistinctBy = string.IsNullOrWhiteSpace(distinctBy) ? null : distinctBy!.Trim();
        if (Kind == EventDetectionRuleKind.DistinctValue && DistinctBy == null) {
            throw new InvalidDataException("Distinct-value rules require DistinctBy.");
        }
        EventDetectionStepDefinition[] steps = (Steps ?? Array.Empty<EventDetectionStepDefinition>())
            .Select(static (step, index) => step?.Snapshot(index) ??
                throw new InvalidDataException($"Steps[{index}] cannot be null."))
            .ToArray();
        if (Kind is EventDetectionRuleKind.Temporal or EventDetectionRuleKind.OrderedTemporal) {
            if (steps.Length < 2) {
                throw new InvalidDataException("Temporal rules require at least two steps.");
            }
            if (steps.Length > MaximumTemporalStepCount) {
                throw new InvalidDataException(
                    $"Temporal rules cannot contain more than {MaximumTemporalStepCount} steps.");
            }
            if (steps.Select(static step => step.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != steps.Length) {
                throw new InvalidDataException("Temporal step names must be unique.");
            }
        } else if (steps.Length != 0) {
            throw new InvalidDataException("Steps are supported only by temporal rules.");
        }
        Steps = steps;
    }

    internal EventDetectionRuleDefinition Snapshot() {
        Validate();
        return new EventDetectionRuleDefinition {
            RuleId = RuleId,
            Version = Version,
            PackId = PackId,
            PackVersion = PackVersion,
            SourceKind = SourceKind,
            SourceId = SourceId,
            SourceStatus = SourceStatus,
            SourceHash = SourceHash,
            License = License,
            Title = Title,
            Description = Description,
            Severity = Severity,
            Confidence = Confidence,
            Kind = Kind,
            EventTypes = EventTypes.ToArray(),
            EventIds = EventIds.ToArray(),
            Channels = Channels.ToArray(),
            Providers = Providers.ToArray(),
            Predicate = Predicate?.Clone(),
            Threshold = Threshold,
            Window = Window,
            GroupBy = GroupBy,
            DistinctBy = DistinctBy,
            Steps = Steps.Select(static (step, index) => step.Snapshot(index)).ToArray(),
            Tags = Tags.ToArray(),
            FalsePositives = FalsePositives.ToArray(),
            References = References.ToArray()
        };
    }

    private static string NormalizeRequired(string? value, string name, int maximumLength) {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength) {
            throw new InvalidDataException($"{name} is required and cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string NormalizeOptional(string? value, string name, int maximumLength) {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength) {
            throw new InvalidDataException($"{name} cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string[] NormalizeText(IReadOnlyList<string>? values, string name) {
        string[] normalized = (values ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Any(static value => value.Length > 2048)) {
            throw new InvalidDataException($"{name} cannot contain values longer than 2048 characters.");
        }
        return normalized;
    }
}
