using System;
using System.Collections.Generic;
using System.Threading;
using EventViewerX.Reports.Evtx;
using EventViewerX.Rules.ActiveDirectory;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Report builder for Windows Security failed logons (4625).
/// </summary>
internal sealed class SecurityFailedLogonsReportBuilder {
    private readonly bool _includeSamples;
    private readonly int _sampleSize;

    private readonly Dictionary<string, long> _byTargetUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byTargetDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byLogonType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byIp = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byWorkstation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byComputer = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byStatusName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _bySubStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _bySubStatusName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byFailureReason = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<SecurityFailedLogonSample> _samples = new();

    private int _scanned;
    private int _matched;
    private DateTime? _minUtc;
    private DateTime? _maxUtc;

    /// <summary>
    /// Creates a new report builder.
    /// </summary>
    /// <param name="includeSamples">When true, captures up to <paramref name="sampleSize"/> sample events.</param>
    /// <param name="sampleSize">Maximum sample events to capture.</param>
    public SecurityFailedLogonsReportBuilder(bool includeSamples, int sampleSize) {
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
        var rule = new ADUserLogonFailed(ev);

        var (targetDomain, targetUser) = SecurityEventText.SplitAccount(rule.ObjectAffected);
        if (string.IsNullOrWhiteSpace(targetUser)) {
            targetUser = Get(data, "TargetUserName") ?? Get(data, "AccountName") ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(targetDomain)) {
            targetDomain = Get(data, "TargetDomainName") ?? string.Empty;
        }

        var logonType = SecurityEventText.FormatLogonType(rule.LogonType);
        var ip = SecurityEventText.NormalizePlaceholder(rule.IpAddress);
        var workstation = SecurityEventText.NormalizePlaceholder(rule.Who);
        var status = SecurityEventText.FormatStatusCode(rule.Status);
        var statusName = rule.Status?.ToString() ?? string.Empty;
        var subStatus = SecurityEventText.FormatSubStatusCode(rule.SubStatus);
        var subStatusName = rule.SubStatus?.ToString() ?? string.Empty;
        var failureReason = SecurityEventText.FormatFailureReason(rule.FailureReason);

        SecurityAggregates.AddCount(_byTargetUser, targetUser);
        SecurityAggregates.AddCount(_byTargetDomain, targetDomain);
        SecurityAggregates.AddCount(_byLogonType, logonType);
        SecurityAggregates.AddCount(_byIp, ip);
        SecurityAggregates.AddCount(_byWorkstation, workstation);
        SecurityAggregates.AddCount(_byStatus, status);
        SecurityAggregates.AddCount(_byStatusName, statusName);
        SecurityAggregates.AddCount(_bySubStatus, subStatus);
        SecurityAggregates.AddCount(_bySubStatusName, subStatusName);
        SecurityAggregates.AddCount(_byFailureReason, failureReason);

        if (_includeSamples && _samples.Count < _sampleSize) {
            _samples.Add(new SecurityFailedLogonSample {
                TimeCreatedUtc = utc,
                Id = ev.Id,
                ComputerName = ev.ComputerName ?? string.Empty,
                TargetUser = targetUser,
                TargetDomain = targetDomain,
                LogonType = logonType,
                IpAddress = ip,
                WorkstationName = workstation,
                Status = status,
                StatusName = statusName,
                SubStatus = subStatus,
                SubStatusName = subStatusName,
                FailureReason = failureReason,
                SubjectUser = Get(data, "SubjectUserName") ?? string.Empty
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
    public static SecurityFailedLogonsReport BuildFromEvents(
        IEnumerable<EventObject> events,
        bool includeSamples,
        int sampleSize,
        CancellationToken cancellationToken = default) {

        var b = new SecurityFailedLogonsReportBuilder(includeSamples, sampleSize);
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
        out SecurityFailedLogonsReport report,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {
        var b = new SecurityFailedLogonsReportBuilder(includeSamples, sampleSize);
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
            report = new SecurityFailedLogonsReport();
            return false;
        }

        report = b.Build();
        report.Truncated = executionInfo.Truncated;
        return true;
    }

    /// <summary>
    /// Builds a report snapshot.
    /// </summary>
    public SecurityFailedLogonsReport Build() {
        return new SecurityFailedLogonsReport {
            Scanned = _scanned,
            Matched = _matched,
            MinUtc = _minUtc,
            MaxUtc = _maxUtc,
            ByTargetUser = Copy(_byTargetUser),
            ByTargetDomain = Copy(_byTargetDomain),
            ByLogonType = Copy(_byLogonType),
            ByIpAddress = Copy(_byIp),
            ByWorkstationName = Copy(_byWorkstation),
            ByComputerName = Copy(_byComputer),
            ByStatus = Copy(_byStatus),
            ByStatusName = Copy(_byStatusName),
            BySubStatus = Copy(_bySubStatus),
            BySubStatusName = Copy(_bySubStatusName),
            ByFailureReason = Copy(_byFailureReason),
            Samples = _samples.ToArray()
        };
    }

    private static Dictionary<string, long> Copy(
        Dictionary<string, long> source) {

        return new Dictionary<string, long>(
            source,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns top target users by count.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, long>> GetTopTargetUsers(int top) => SecurityAggregates.TopStringPairs(_byTargetUser, top);

    private static string? Get(Dictionary<string, string> dict, string key) {
        return dict.TryGetValue(key, out var v) ? v : null;
    }
}
