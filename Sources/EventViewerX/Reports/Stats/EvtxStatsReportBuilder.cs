using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventViewerX.Reports;
using EventViewerX.Reports.Evtx;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Report builder for basic EVTX statistics (counts by ID/provider/level/computer, min/max times).
/// </summary>
internal sealed class EvtxStatsReportBuilder {
    private readonly Dictionary<int, long> _byEventId = new();
    private readonly Dictionary<string, long> _byProviderName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byComputerName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, EvtxLevelStats> _byLevel = new();

    private int _scanned;
    private long _eventsWithoutLevel;
    private DateTime? _minUtc;
    private DateTime? _maxUtc;

    /// <summary>Number of scanned events passed into the builder.</summary>
    public int Scanned => _scanned;
    /// <summary>Minimum event time (UTC) among scanned events.</summary>
    public DateTime? MinUtc => _minUtc;
    /// <summary>Maximum event time (UTC) among scanned events.</summary>
    public DateTime? MaxUtc => _maxUtc;
    /// <summary>Number of scanned events that did not expose a level.</summary>
    public long EventsWithoutLevel => _eventsWithoutLevel;

    /// <summary>
    /// Adds a detailed event object to the report.
    /// </summary>
    public void Add(EventObject ev) {
        if (ev is null) throw new ArgumentNullException(nameof(ev));

        Add(
            id: ev.Id,
            timeCreatedUtc: ev.TimeCreated == DateTime.MinValue
                ? DateTime.MinValue
                : ev.TimeCreated.ToUniversalTime(),
            providerName: ev.ProviderName,
            computerName: ev.ComputerName,
            level: ev.Level,
            levelDisplayName: ev.LevelDisplayName);
    }

    /// <summary>
    /// Adds a single event's key fields to the report.
    /// </summary>
    public void Add(
        int id,
        DateTime timeCreatedUtc,
        string? providerName,
        string? computerName,
        int? level,
        string? levelDisplayName) {

        _scanned++;

        if (timeCreatedUtc != DateTime.MinValue) {
            if (!_minUtc.HasValue ||
                timeCreatedUtc < _minUtc.Value) {
                _minUtc = timeCreatedUtc;
            }
            if (!_maxUtc.HasValue ||
                timeCreatedUtc > _maxUtc.Value) {
                _maxUtc = timeCreatedUtc;
            }
        }

        ReportAggregates.AddCount(_byEventId, id);
        ReportAggregates.AddCount(_byProviderName, providerName, useUnknownPlaceholder: true);
        EvtxStatsAggregates.AddComputerCount(_byComputerName, computerName);
        if (level.HasValue) {
            EvtxStatsAggregates.AddLevelCount(
                _byLevel,
                level.Value,
                levelDisplayName);
        } else {
            _eventsWithoutLevel++;
        }
    }

    /// <summary>
    /// Adds multiple events to the report.
    /// </summary>
    /// <param name="events">Events to aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
    /// Builds a report snapshot.
    /// </summary>
    public EvtxStatsReport Build() {
        return new EvtxStatsReport {
            Scanned = _scanned,
            EventsWithoutLevel = _eventsWithoutLevel,
            MinUtc = _minUtc,
            MaxUtc = _maxUtc,
            ByEventId =
                new Dictionary<int, long>(
                    _byEventId),
            ByProviderName =
                new Dictionary<string, long>(
                    _byProviderName,
                    StringComparer.OrdinalIgnoreCase),
            ByComputerName =
                new Dictionary<string, long>(
                    _byComputerName,
                    StringComparer.OrdinalIgnoreCase),
            ByLevel = _byLevel.ToDictionary(
                static pair => pair.Key,
                static pair =>
                    new EvtxLevelStats(
                        pair.Value.Level,
                        pair.Value.LevelDisplayName) {
                            Count = pair.Value.Count
                        })
        };
    }

    /// <summary>
    /// Convenience API: reads an EVTX file using <see cref="EventLogEngine"/> and returns a stats report.
    /// </summary>
    public static EvtxStatsReport BuildFromFile(
        string filePath,
        List<int>? eventIds = null,
        string? providerName = null,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null,
        int maxEvents = 0,
        bool oldestFirst = false,
        CancellationToken cancellationToken = default) {
        var request = new EvtxQueryRequest {
            FilePath = filePath,
            EventIds = eventIds,
            ProviderName = providerName,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            MaxEvents = maxEvents,
            OldestFirst = oldestFirst,
            ReadMode = EventReadMode.Metadata
        };

        if (!TryBuildFromFile(request, out var report, out var failure, cancellationToken)) {
            throw new InvalidOperationException(failure?.Message ?? "EVTX query failed.");
        }

        return report;
    }

    /// <summary>
    /// Convenience API: reads an EVTX file using an <see cref="EvtxQueryRequest"/> and returns a stats report.
    /// </summary>
    /// <param name="request">EVTX query request.</param>
    /// <param name="report">Built report when successful.</param>
    /// <param name="failure">Failure details when query fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when query succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryBuildFromFile(
        EvtxQueryRequest? request,
        out EvtxStatsReport report,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {
        var builder = new EvtxStatsReportBuilder();
        if (!EvtxQueryExecutor.TryForEachEventWithInfo(
                request,
                ev => {
                    builder.Add(ev);
                    return true;
                },
                out EvtxQueryExecutionInfo executionInfo,
                out failure,
                cancellationToken,
                readModeOverride: EventReadMode.Metadata)) {
            report = new EvtxStatsReport();
            return false;
        }

        report = builder.Build();
        report.Truncated = executionInfo.Truncated;
        return true;
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> event IDs by count.
    /// </summary>
    public IReadOnlyList<KeyValuePair<int, long>> GetTopEventIds(int top) => ReportAggregates.TopIntPairs(_byEventId, top);

    /// <summary>
    /// Returns the top <paramref name="top"/> provider names by count.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, long>> GetTopProviders(int top) => ReportAggregates.TopStringPairs(_byProviderName, top);

    /// <summary>
    /// Returns the top <paramref name="top"/> computer names by count.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, long>> GetTopComputers(int top) => ReportAggregates.TopStringPairs(_byComputerName, top);

    /// <summary>
    /// Returns the top <paramref name="top"/> levels by count.
    /// </summary>
    public IReadOnlyList<EvtxLevelStats> GetTopLevels(int top) {
        if (top <= 0) return Array.Empty<EvtxLevelStats>();
        return _byLevel.Values
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Level)
            .Take(top)
            .ToList();
    }

}
