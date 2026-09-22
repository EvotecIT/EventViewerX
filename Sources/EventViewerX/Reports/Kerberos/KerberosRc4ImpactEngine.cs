using EventViewerX.Reporting;
using EventViewerX.Rules.Kerberos;

namespace EventViewerX;

/// <summary>Builds an event-local RC4 enforcement view without loading every event into memory.</summary>
public static class KerberosRc4ImpactEngine {
    /// <summary>Creates a bounded accumulator for live, saved, or stored KDCsvc rows.</summary>
    public static KerberosRc4ImpactAccumulator CreateAccumulator(
        int maximumGroups = 10_000,
        int maximumEvidencePerGroup = 25) => new(maximumGroups, maximumEvidencePerGroup);

    /// <summary>Analyzes a finite report and preserves its completeness evidence.</summary>
    public static KerberosRc4ImpactReport Analyze(
        EventReport report,
        int maximumGroups = 10_000,
        int maximumEvidencePerGroup = 25) {
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        KerberosRc4ImpactAccumulator accumulator = CreateAccumulator(maximumGroups, maximumEvidencePerGroup);
        foreach (EventReportRow row in report.Rows) {
            accumulator.Add(row);
        }
        EventReportCoverage[] failedSources = report.Coverage
            .Where(static coverage => !coverage.Succeeded)
            .ToArray();
        bool sourceComplete = !report.ScanLimitReached && failedSources.Length == 0 &&
            string.IsNullOrWhiteSpace(report.CompletenessDiagnostic);
        string? failures = failedSources.Length == 0 ? null :
            $"{failedSources.Length} source query failed: " +
            string.Join("; ", failedSources.Take(10).Select(static coverage =>
                $"{coverage.MachineName}/{coverage.LogName} ({coverage.Status}): {coverage.Detail}")) +
            (failedSources.Length > 10 ? "; additional failures omitted" : string.Empty);
        string? diagnostic = EventCompletenessDiagnostic.Compose(
            report.CompletenessDiagnostic,
            failures,
            sourceComplete ? null : "The source query was incomplete; RC4 impact output cannot be treated as exhaustive.");
        return accumulator.Complete(sourceComplete, diagnostic);
    }
}

/// <summary>Bounded, streaming accumulator for KDCsvc 201-209 change evidence.</summary>
public sealed class KerberosRc4ImpactAccumulator {
    private readonly int _maximumGroups;
    private readonly int _maximumEvidencePerGroup;
    private readonly Dictionary<string, GroupState> _groups = new(StringComparer.Ordinal);
    private long _eventsObserved;

    internal KerberosRc4ImpactAccumulator(int maximumGroups, int maximumEvidencePerGroup) {
        if (maximumGroups < 1) {
            throw new ArgumentOutOfRangeException(nameof(maximumGroups));
        }
        if (maximumEvidencePerGroup < 1) {
            throw new ArgumentOutOfRangeException(nameof(maximumEvidencePerGroup));
        }
        _maximumGroups = maximumGroups;
        _maximumEvidencePerGroup = maximumEvidencePerGroup;
    }

    /// <summary>Adds one normalized event row; unrelated rows are ignored.</summary>
    public void Add(EventReportRow row) {
        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }
        if (!string.Equals(row.Type, nameof(EventType.KerberosKdcRc4Audit), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(row.Provider, "Kdcsvc", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(row.SourceLog, "System", StringComparison.OrdinalIgnoreCase)) {
            return;
        }
        (KerberosKdcRc4Issue issue, KerberosKdcRc4Disposition disposition) =
            KerberosKdcRc4Audit.Classify(row.EventId);
        if (disposition == KerberosKdcRc4Disposition.Unknown) {
            return;
        }
        KerberosRc4ImpactState impact = disposition switch {
            KerberosKdcRc4Disposition.AuditWarning => KerberosRc4ImpactState.MayFailUnderEnforcement,
            KerberosKdcRc4Disposition.EnforcementBlock => KerberosRc4ImpactState.AlreadyBlocked,
            _ => KerberosRc4ImpactState.ExplicitInsecureDefault
        };
        string controller = NonEmpty(row.SourceComputer);
        string account = Read(row, "AccountName");
        string service = Read(row, "ServiceName");
        string serviceSid = Read(row, "ServiceSid");
        string key = string.Join("\0", new[] {
            controller.ToUpperInvariant(), account.ToUpperInvariant(), service.ToUpperInvariant(),
            serviceSid.ToUpperInvariant(), issue.ToString(), impact.ToString()
        });
        if (!_groups.TryGetValue(key, out GroupState? state)) {
            if (_groups.Count >= _maximumGroups) {
                throw new InvalidOperationException(
                    $"Kerberos RC4 impact exceeded MaximumGroups {_maximumGroups:N0}; narrow the event window or increase the explicit group limit.");
            }
            state = new GroupState(controller, account, service, serviceSid, issue, impact);
            _groups.Add(key, state);
        }
        state.Count++;
        _eventsObserved++;
        DateTime eventTimeUtc = row.TimeCreated.ToUniversalTime();
        if (eventTimeUtc > state.LatestEventUtc) {
            state.LatestEventUtc = eventTimeUtc;
        }
        if (!string.IsNullOrWhiteSpace(row.ObservationIdentity) &&
            !state.Evidence.Contains(row.ObservationIdentity, StringComparer.Ordinal)) {
            if (state.Evidence.Count < _maximumEvidencePerGroup) {
                state.Evidence.Add(row.ObservationIdentity);
            } else {
                state.EvidenceTruncated = true;
            }
        }
    }

    /// <summary>Finishes the view and records whether the selected input window was exhaustive.</summary>
    public KerberosRc4ImpactReport Complete(bool selectedWindowComplete, string? completenessDiagnostic = null) {
        KerberosRc4ImpactGroup[] groups = _groups.Values
            .OrderBy(static state => state.Impact)
            .ThenBy(static state => state.DomainController, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static state => state.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static state => state.AccountName, StringComparer.OrdinalIgnoreCase)
            .Select(static state => new KerberosRc4ImpactGroup(
                state.DomainController, state.AccountName, state.ServiceName, state.ServiceSid,
                state.Issue, state.Impact, state.Count, state.LatestEventUtc,
                state.Evidence.ToArray(), state.EvidenceTruncated))
            .ToArray();
        return new KerberosRc4ImpactReport(groups, _eventsObserved, selectedWindowComplete, completenessDiagnostic);
    }

    private static string Read(EventReportRow row, string name) =>
        row.Values.TryGetValue(name, out object? value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
            : string.Empty;

    private static string NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<unknown>" : value!.Trim();

    private sealed class GroupState {
        internal GroupState(string domainController, string accountName, string serviceName, string serviceSid,
            KerberosKdcRc4Issue issue, KerberosRc4ImpactState impact) {
            DomainController = domainController;
            AccountName = accountName;
            ServiceName = serviceName;
            ServiceSid = serviceSid;
            Issue = issue;
            Impact = impact;
        }
        internal string DomainController { get; }
        internal string AccountName { get; }
        internal string ServiceName { get; }
        internal string ServiceSid { get; }
        internal KerberosKdcRc4Issue Issue { get; }
        internal KerberosRc4ImpactState Impact { get; }
        internal long Count { get; set; }
        internal DateTime LatestEventUtc { get; set; }
        internal List<string> Evidence { get; } = new();
        internal bool EvidenceTruncated { get; set; }
    }
}
