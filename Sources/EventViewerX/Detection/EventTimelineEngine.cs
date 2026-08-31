using System.Globalization;

namespace EventViewerX;

/// <summary>Builds bounded, explainable timelines over observations and findings.</summary>
public static class EventTimelineEngine {
    private static readonly IReadOnlyDictionary<EventPivotKind, string[]> PivotFields =
        new Dictionary<EventPivotKind, string[]> {
            [EventPivotKind.Actor] = new[] { "Who", "SubjectUserName", "SubjectAccountName" },
            [EventPivotKind.Target] = new[] { "ObjectAffected", "TargetUserName", "MemberName", "ObjectName" },
            [EventPivotKind.Host] = new[] { "SourceComputer", "Computer", "CollectorComputer", "WorkstationName" },
            [EventPivotKind.Account] = new[] { "Who", "ObjectAffected", "SubjectUserName", "TargetUserName", "MemberName" },
            [EventPivotKind.Sid] = new[] { "SubjectUserSid", "TargetUserSid", "MemberSid", "ObjectSid" },
            [EventPivotKind.IpAddress] = new[] { "IpAddress", "SourceNetworkAddress", "SourceIp", "DestinationIp" },
            [EventPivotKind.Process] = new[] { "ProcessName", "NewProcessName", "ProcessId", "NewProcessId" },
            [EventPivotKind.Activity] = new[] { "ActivityId", "RelatedActivityId" },
            [EventPivotKind.Logon] = new[] { "LogonId", "TargetLogonId", "SubjectLogonId" },
            [EventPivotKind.Transaction] = new[] { "TransactionId", "CorrelationId", "OperationId" }
        };

    /// <summary>Creates a timeline without rerunning the source query or detection plan.</summary>
    public static EventTimeline Create(
        IEnumerable<EventObservation>? observations,
        IEnumerable<EventDetectionFinding>? findings,
        EventTimelineOptions? options = null) {

        options ??= new EventTimelineOptions();
        string? pivotValue = string.IsNullOrWhiteSpace(options.PivotValue) ? null : options.PivotValue!.Trim();
        var entries = new List<EventTimelineEntry>();
        if (options.IncludeObservations) {
            foreach (EventObservation observation in observations ?? Array.Empty<EventObservation>()) {
                if (observation == null) {
                    throw new ArgumentException("Observations cannot contain null values.", nameof(observations));
                }
                IReadOnlyList<EventPivot> pivots = ExtractPivots(observation);
                entries.Add(new EventTimelineEntry(
                    EventTimelineEntryKind.Observation,
                    observation.Identity,
                    $"{observation.TypeName} ({observation.EventId})",
                    observation.EventTimeUtc,
                    observation.ReceivedTimeUtc,
                    observation.ProcessedTimeUtc,
                    string.Empty,
                    severity: null,
                    new[] { observation.Identity },
                    pivots));
            }
        }
        if (options.IncludeFindings) {
            foreach (EventDetectionFinding finding in findings ?? Array.Empty<EventDetectionFinding>()) {
                if (finding == null) {
                    throw new ArgumentException("Findings cannot contain null values.", nameof(findings));
                }
                EventObservation[] evidence = finding.Evidence.ToArray();
                IReadOnlyList<EventPivot> pivots = evidence.SelectMany(ExtractPivots)
                    .GroupBy(static pivot => (pivot.Kind, pivot.Value), PivotIdentityComparer.Instance)
                    .Select(static group => group.First())
                    .ToArray();
                string identity = finding.RuleId + ":" + string.Join(";", finding.EvidenceIdentities);
                entries.Add(new EventTimelineEntry(
                    EventTimelineEntryKind.Finding,
                    identity,
                    finding.Title,
                    finding.StartTimeUtc,
                    evidence.Max(static item => item.ReceivedTimeUtc),
                    evidence.Max(static item => item.ProcessedTimeUtc),
                    finding.RuleId,
                    finding.Severity,
                    finding.EvidenceIdentities,
                    pivots));
            }
        }
        EventTimelineEntry[] filtered = entries
            .Where(entry => MatchesPivot(entry, options.PivotKind, pivotValue))
            .OrderBy(static entry => entry.EventTimeUtc)
            .ThenBy(static entry => entry.Kind)
            .ThenBy(static entry => entry.Identity, StringComparer.Ordinal)
            .ToArray();
        return new EventTimeline(filtered, options.PivotKind, pivotValue);
    }

    /// <summary>Extracts deduplicated actor, target, host, account, SID, IP, process, activity, logon, and transaction pivots.</summary>
    public static IReadOnlyList<EventPivot> ExtractPivots(EventObservation observation) {
        if (observation == null) {
            throw new ArgumentNullException(nameof(observation));
        }
        var pivots = new List<EventPivot>();
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<EventPivotKind, string[]> mapping in PivotFields) {
            foreach (string field in mapping.Value) {
                if (!observation.Fields.TryGetValue(field, out object? value) || value == null) {
                    continue;
                }
                string text = value is IFormattable formattable
                    ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
                    : value.ToString() ?? string.Empty;
                if (text.Length != 0 && emitted.Add(mapping.Key + "\0" + text)) {
                    pivots.Add(new EventPivot(mapping.Key, field, text));
                }
            }
        }
        return Array.AsReadOnly(pivots.ToArray());
    }

    private static bool MatchesPivot(
        EventTimelineEntry entry,
        EventPivotKind? kind,
        string? value) {

        if (!kind.HasValue && value == null) {
            return true;
        }
        return entry.Pivots.Any(pivot =>
            (!kind.HasValue || pivot.Kind == kind.Value) &&
            (value == null || string.Equals(pivot.Value, value, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class PivotIdentityComparer : IEqualityComparer<(EventPivotKind Kind, string Value)> {
        internal static PivotIdentityComparer Instance { get; } = new();

        public bool Equals(
            (EventPivotKind Kind, string Value) left,
            (EventPivotKind Kind, string Value) right) =>
            left.Kind == right.Kind && string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((EventPivotKind Kind, string Value) value) {
            unchecked {
                return ((int)value.Kind * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(value.Value);
            }
        }
    }
}
