using System;
using System.Collections.Generic;
using System.Threading;
using EventViewerX.Reports.Evtx;
using EventViewerX.Rules.ActiveDirectory;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Report builder for Windows Security account lockouts (4740).
/// </summary>
internal sealed class SecurityAccountLockoutsReportBuilder {
    private readonly bool _includeSamples;
    private readonly int _sampleSize;

    private readonly Dictionary<string, long> _byTargetUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byTargetDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byCallerComputer = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byComputer = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _bySubjectUser = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<SecurityAccountLockoutSample> _samples = new();

    private int _scanned;
    private int _matched;
    private DateTime? _minUtc;
    private DateTime? _maxUtc;

    /// <summary>
    /// Creates a new report builder.
    /// </summary>
    /// <param name="includeSamples">When true, captures up to <paramref name="sampleSize"/> sample events.</param>
    /// <param name="sampleSize">Maximum sample events to capture.</param>
    public SecurityAccountLockoutsReportBuilder(bool includeSamples, int sampleSize) {
        _includeSamples = includeSamples;
        _sampleSize = Math.Max(0, sampleSize);
    }

    /// <summary>Number of scanned events passed into the builder.</summary>
    public int Scanned => _scanned;
    /// <summary>Number of matched events.</summary>
    public int Matched => _matched;
    /// <summary>Minimum event time (UTC) among matched events.</summary>
    public DateTime? MinUtc => _minUtc;
    /// <summary>Maximum event time (UTC) among matched events.</summary>
    public DateTime? MaxUtc => _maxUtc;

    /// <summary>
    /// Adds an event to the report.
    /// </summary>
    public void Add(EventObject ev) {
        _scanned++;
        _matched++;

        DateTime? utc =
            SecurityAggregates.NormalizeUtc(
                ev.TimeCreated);
        if (utc.HasValue) {
            if (!_minUtc.HasValue ||
                utc.Value < _minUtc.Value) {
                _minUtc = utc.Value;
            }
            if (!_maxUtc.HasValue ||
                utc.Value > _maxUtc.Value) {
                _maxUtc = utc.Value;
            }
        }

        SecurityAggregates.AddCount(_byComputer, ev.ComputerName ?? string.Empty);

        var data = ev.Data ?? new Dictionary<string, string>();
        var rule = new ADUserLockouts(ev);

        var (targetDomain, targetUser) = SecurityEventText.SplitAccount(rule.UserAffected);
        if (string.IsNullOrWhiteSpace(targetUser)) {
            targetUser = Get(data, "TargetUserName") ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(targetDomain)) {
            targetDomain = Get(data, "TargetDomainName") ?? string.Empty;
        }

        var caller = SecurityEventText.NormalizePlaceholder(Get(data, "CallerComputerName") ?? string.Empty);

        var subjectUser = rule.Who;
        if (string.IsNullOrWhiteSpace(subjectUser)) {
            subjectUser = Get(data, "SubjectUserName") ?? string.Empty;
        }

        SecurityAggregates.AddCount(_byTargetUser, targetUser);
        SecurityAggregates.AddCount(_byTargetDomain, targetDomain);
        SecurityAggregates.AddCount(_byCallerComputer, caller);
        SecurityAggregates.AddCount(_bySubjectUser, subjectUser);

        if (_includeSamples && _samples.Count < _sampleSize) {
            _samples.Add(new SecurityAccountLockoutSample {
                TimeCreatedUtc = utc,
                Id = ev.Id,
                ComputerName = ev.ComputerName ?? string.Empty,
                TargetUser = targetUser,
                TargetDomain = targetDomain,
                CallerComputerName = caller,
                SubjectUser = subjectUser
            });
        }
    }

    /// <summary>
    /// Adds multiple events to the report.
    /// </summary>
    public void AddRange(IEnumerable<EventObject> events, CancellationToken cancellationToken = default) {
        if (events is null) {
            return;
        }
        foreach (var ev in events) {
            cancellationToken.ThrowIfCancellationRequested();
            Add(ev);
        }
    }

    /// <summary>
    /// Builds a report directly from an event sequence.
    /// </summary>
    public static SecurityAccountLockoutsReport BuildFromEvents(
        IEnumerable<EventObject> events,
        bool includeSamples,
        int sampleSize,
        CancellationToken cancellationToken = default) {

        var b = new SecurityAccountLockoutsReportBuilder(includeSamples, sampleSize);
        b.AddRange(events, cancellationToken);
        return b.Build();
    }

    /// <summary>
    /// Builds a report directly from an EVTX query request.
    /// </summary>
    /// <param name="request">EVTX query request.</param>
    /// <param name="includeSamples">When true, captures up to <paramref name="sampleSize"/> sample events.</param>
    /// <param name="sampleSize">Maximum sample events to capture.</param>
    /// <param name="report">Built report when successful.</param>
    /// <param name="failure">Failure details when query fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when query succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryBuildFromFile(
        EvtxQueryRequest? request,
        bool includeSamples,
        int sampleSize,
        out SecurityAccountLockoutsReport report,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {
        var b = new SecurityAccountLockoutsReportBuilder(includeSamples, sampleSize);
        if (!EvtxQueryExecutor.TryForEachEventWithInfo(
                request,
                ev => {
                    b.Add(ev);
                    return true;
                },
                out EvtxQueryExecutionInfo executionInfo,
                out failure,
                cancellationToken,
                readModeOverride: EventReadMode.StructuredData)) {
            report = new SecurityAccountLockoutsReport();
            return false;
        }

        report = b.Build();
        report.Truncated = executionInfo.Truncated;
        return true;
    }

    /// <summary>
    /// Builds a report snapshot.
    /// </summary>
    public SecurityAccountLockoutsReport Build() {
        return new SecurityAccountLockoutsReport {
            Scanned = _scanned,
            Matched = _matched,
            MinUtc = _minUtc,
            MaxUtc = _maxUtc,
            ByTargetUser =
                new Dictionary<string, long>(
                    _byTargetUser,
                    StringComparer.OrdinalIgnoreCase),
            ByTargetDomain =
                new Dictionary<string, long>(
                    _byTargetDomain,
                    StringComparer.OrdinalIgnoreCase),
            ByCallerComputerName =
                new Dictionary<string, long>(
                    _byCallerComputer,
                    StringComparer.OrdinalIgnoreCase),
            BySubjectUser =
                new Dictionary<string, long>(
                    _bySubjectUser,
                    StringComparer.OrdinalIgnoreCase),
            ByComputerName =
                new Dictionary<string, long>(
                    _byComputer,
                    StringComparer.OrdinalIgnoreCase),
            Samples = _samples.ToArray()
        };
    }

    /// <summary>
    /// Returns top target users by count.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, long>> GetTopTargetUsers(int top) => SecurityAggregates.TopStringPairs(_byTargetUser, top);

    private static string? Get(Dictionary<string, string> dict, string key) {
        return dict.TryGetValue(key, out var v) ? v : null;
    }
}
