using System;
using System.Collections.Generic;
using System.Threading;
using EventViewerX.Reports.Evtx;
using EventViewerX.Rules.ActiveDirectory;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Report builder for Windows Security logon-related events (4624/4625/4634/4647).
/// </summary>
internal sealed class SecurityUserLogonsReportBuilder {
    private readonly bool _includeSamples;
    private readonly int _sampleSize;
    private readonly IReadOnlyList<int> _eventIds;

    private readonly Dictionary<int, long> _byEventId = new();
    private readonly Dictionary<string, long> _byTargetUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byTargetDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byLogonType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byIp = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byWorkstation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byComputer = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<SecurityUserLogonSample> _samples = new();

    private int _scanned;
    private int _matched;
    private DateTime? _minUtc;
    private DateTime? _maxUtc;

    /// <summary>
    /// Creates a new report builder.
    /// </summary>
    /// <param name="includeSamples">When true, captures up to <paramref name="sampleSize"/> sample events.</param>
    /// <param name="sampleSize">Maximum sample events to capture.</param>
    /// <param name="eventIds">Event IDs included by the caller (for reporting).</param>
    public SecurityUserLogonsReportBuilder(bool includeSamples, int sampleSize, IReadOnlyList<int> eventIds) {
        _includeSamples = includeSamples;
        _sampleSize = Math.Max(0, sampleSize);
        _eventIds = eventIds ?? Array.Empty<int>();
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

        SecurityAggregates.AddCount(_byEventId, ev.Id);
        SecurityAggregates.AddCount(_byComputer, ev.ComputerName ?? string.Empty);

        var data = ev.Data ?? new Dictionary<string, string>();

        string targetUser;
        string targetDomain;
        string logonType;
        string ip;
        string workstation;

        switch (ev.Id) {
            case 4624: {
                var rule = new ADUserLogon(ev);
                var acc = SecurityEventText.SplitAccount(rule.ObjectAffected);
                targetDomain = acc.Domain;
                targetUser = acc.User;
                logonType = SecurityEventText.FormatLogonType(rule.LogonType);
                ip = SecurityEventText.NormalizePlaceholder(rule.IpAddress);
                workstation = SecurityEventText.NormalizePlaceholder(Get(data, "WorkstationName"));
                break;
            }
            case 4625: {
                var rule = new ADUserLogonFailed(ev);
                var acc = SecurityEventText.SplitAccount(rule.ObjectAffected);
                targetDomain = acc.Domain;
                targetUser = acc.User;
                logonType = SecurityEventText.FormatLogonType(rule.LogonType);
                ip = SecurityEventText.NormalizePlaceholder(rule.IpAddress);
                workstation = SecurityEventText.NormalizePlaceholder(rule.Who);
                break;
            }
            default: {
                targetUser = Get(data, "TargetUserName") ?? Get(data, "AccountName") ?? string.Empty;
                targetDomain = Get(data, "TargetDomainName") ?? string.Empty;
                logonType = Get(data, "LogonType") ?? string.Empty;
                ip = Get(data, "IpAddress") ?? Get(data, "ClientAddress") ?? string.Empty;
                workstation = Get(data, "WorkstationName") ?? string.Empty;
                workstation = SecurityEventText.NormalizePlaceholder(workstation);
                ip = SecurityEventText.NormalizePlaceholder(ip);
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(targetUser)) {
            targetUser = Get(data, "TargetUserName") ?? Get(data, "AccountName") ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(targetDomain)) {
            targetDomain = Get(data, "TargetDomainName") ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(logonType)) {
            logonType = Get(data, "LogonType") ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(ip)) {
            ip = SecurityEventText.NormalizePlaceholder(Get(data, "IpAddress") ?? Get(data, "ClientAddress"));
        }
        if (string.IsNullOrWhiteSpace(workstation)) {
            workstation = SecurityEventText.NormalizePlaceholder(Get(data, "WorkstationName"));
        }

        SecurityAggregates.AddCount(_byTargetUser, targetUser);
        SecurityAggregates.AddCount(_byTargetDomain, targetDomain);
        SecurityAggregates.AddCount(_byLogonType, logonType);
        SecurityAggregates.AddCount(_byIp, ip);
        SecurityAggregates.AddCount(_byWorkstation, workstation);

        if (_includeSamples && _samples.Count < _sampleSize) {
            _samples.Add(new SecurityUserLogonSample {
                TimeCreatedUtc = utc,
                Id = ev.Id,
                ComputerName = ev.ComputerName ?? string.Empty,
                TargetUser = targetUser,
                TargetDomain = targetDomain,
                LogonType = logonType,
                IpAddress = ip,
                WorkstationName = workstation,
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
    public static SecurityUserLogonsReport BuildFromEvents(
        IEnumerable<EventObject> events,
        IReadOnlyList<int> eventIds,
        bool includeSamples,
        int sampleSize,
        CancellationToken cancellationToken = default) {

        var b = new SecurityUserLogonsReportBuilder(includeSamples, sampleSize, eventIds ?? Array.Empty<int>());
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
        out SecurityUserLogonsReport report,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {
        var eventIds = request?.EventIds ?? Array.Empty<int>();
        var b = new SecurityUserLogonsReportBuilder(includeSamples, sampleSize, eventIds);
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
            report = new SecurityUserLogonsReport();
            return false;
        }

        report = b.Build();
        report.Truncated = executionInfo.Truncated;
        return true;
    }

    /// <summary>
    /// Builds a report snapshot.
    /// </summary>
    public SecurityUserLogonsReport Build() {
        return new SecurityUserLogonsReport {
            Scanned = _scanned,
            Matched = _matched,
            MinUtc = _minUtc,
            MaxUtc = _maxUtc,
            EventIds = _eventIds.ToArray(),
            ByEventId =
                new Dictionary<int, long>(
                    _byEventId),
            ByTargetUser = Copy(_byTargetUser),
            ByTargetDomain = Copy(_byTargetDomain),
            ByLogonType = Copy(_byLogonType),
            ByIpAddress = Copy(_byIp),
            ByWorkstationName = Copy(_byWorkstation),
            ByComputerName = Copy(_byComputer),
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
