using System.Net;

namespace EventViewerX.Reporting;

/// <summary>Defines one event query whose snapshot can be rendered to several output formats.</summary>
public sealed class EventReportRequest {
    /// <summary>Creates a typed report request.</summary>
    public static EventReportRequest ForTypes(params EventType[] types) => new() { Types = types };

    /// <summary>Creates a generic channel report request.</summary>
    public static EventReportRequest ForLog(string logName) => new() { LogName = logName };

    /// <summary>Creates a generic offline event-log report request.</summary>
    public static EventReportRequest ForFiles(params string[] paths) => new() { Paths = paths };

    /// <summary>Creates a custom-definition report request.</summary>
    public static EventReportRequest ForDefinition(EventDefinition definition) => new() { Definition = definition };

    /// <summary>Built-in leaf or composite event types. Mutually exclusive with <see cref="LogName"/>.</summary>
    public IReadOnlyList<EventType>? Types { get; set; }

    /// <summary>Generic Windows event channel. Used only when <see cref="Types"/> is empty.</summary>
    public string? LogName { get; set; }

    /// <summary>
    /// Offline event-log paths. Paths may be used alone for a generic report or
    /// combined with Types or Definition, which continue to own query semantics.
    /// </summary>
    public IReadOnlyList<string>? Paths { get; set; }

    /// <summary>Declarative custom definition. Mutually exclusive with Types and LogName.</summary>
    public EventDefinition? Definition { get; set; }

    /// <summary>Optional exact predicate for a built-in or custom typed definition.</summary>
    public EventPredicate? Predicate { get; set; }

    /// <summary>Event IDs for a generic channel query.</summary>
    public IReadOnlyCollection<int>? EventIds { get; set; }

    /// <summary>Exact record identifiers, including event-triggered task handoff.</summary>
    public IReadOnlyCollection<long>? RecordIds { get; set; }

    /// <summary>Direct local or remote query targets.</summary>
    public IReadOnlyList<string?>? MachineNames { get; set; }

    /// <summary>Collector computers whose ForwardedEvents channel should be queried.</summary>
    public IReadOnlyList<string?>? Collectors { get; set; }

    /// <summary>Collector channel, normally ForwardedEvents.</summary>
    public string CollectorLogName { get; set; } = "ForwardedEvents";

    /// <summary>Absolute start time.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Absolute end time.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Relative time window.</summary>
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>Maximum result count. Zero is unlimited.</summary>
    public long MaxEvents { get; set; }

    /// <summary>Maximum raw candidates evaluated for typed queries. Zero is unlimited.</summary>
    public long MaxCandidates { get; set; }

    /// <summary>Maximum independent sources opened concurrently.</summary>
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>Reads the oldest matching events first.</summary>
    public bool Oldest { get; set; }

    /// <summary>
    /// Optional per-target and per-container exclusive record checkpoint used
    /// by durable collection hosts.
    /// </summary>
    public Func<string?, string, long?>? MinimumRecordIdExclusiveResolver { get; set; }

    /// <summary>Resolves IP addresses through DnsClientX after typed projection.</summary>
    public bool ResolveDns { get; set; }

    /// <summary>Remote Windows Event Log credential.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Continues healthy remote targets after an expected remote-target failure.</summary>
    public bool ContinueOnRemoteFailure { get; set; } = true;

    /// <summary>Report title.</summary>
    public string? Title { get; set; }

    internal void Validate() {
        bool hasTypes = Types != null && Types.Count > 0;
        bool hasLog = !string.IsNullOrWhiteSpace(LogName);
        bool hasPaths = Paths != null && Paths.Count > 0;
        bool hasDefinition = Definition != null;
        int logicalDefinitions = (hasTypes ? 1 : 0) + (hasLog ? 1 : 0) + (hasDefinition ? 1 : 0);
        if (logicalDefinitions > 1 || logicalDefinitions == 0 && !hasPaths) {
            throw new InvalidOperationException("Specify one of Types, LogName, Definition, or standalone Paths.");
        }
        if (hasLog && hasPaths) {
            throw new InvalidOperationException("LogName and offline Paths are mutually exclusive; use Paths alone for a generic offline query.");
        }
        if (Collectors != null && Collectors.Count > 0 && MachineNames != null && MachineNames.Count > 0) {
            throw new InvalidOperationException("Collectors and MachineNames are mutually exclusive.");
        }
        if (hasLog && Collectors != null && Collectors.Count > 0) {
            throw new InvalidOperationException("Collectors require a built-in or custom definition because the definition owns source-channel routing.");
        }
        if (hasPaths && (MachineNames != null && MachineNames.Count > 0 || Collectors != null && Collectors.Count > 0 || Credential != null)) {
            throw new InvalidOperationException("Offline Paths cannot be combined with remote targets, collectors, or credentials.");
        }
        if (hasPaths && Paths!.Any(static path => string.IsNullOrWhiteSpace(path))) {
            throw new InvalidOperationException("Offline Paths cannot contain empty values.");
        }
        if (MaxEvents < 0 || MaxCandidates < 0) {
            throw new InvalidOperationException("Event limits cannot be negative.");
        }
        if (MaxConcurrency is < 1 or > EventLogLimits.MaximumConcurrency) {
            throw new InvalidOperationException(
                $"MaxConcurrency must be between 1 and {EventLogLimits.MaximumConcurrency}.");
        }
        if (Predicate != null && !hasTypes && !hasDefinition) {
            throw new InvalidOperationException("Predicate requires Types or Definition.");
        }
        if (EventIds != null && EventIds.Count > 0 && (hasTypes || hasDefinition)) {
            throw new InvalidOperationException(
                "EventIds are available only for generic LogName or standalone Paths queries because typed definitions own source event IDs. " +
                "Use a typed EventId predicate to further restrict typed events.");
        }
        Predicate?.Validate();
        Definition?.Validate();
    }
}
