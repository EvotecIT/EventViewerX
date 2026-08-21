using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>Creates a homogeneous renderable report from stored calendar summaries.</summary>
    public async Task<EventReport> CreateSummaryReportAsync(
        EventStoreQuery query,
        EventStoreSummaryPeriod period,
        string? title = null,
        CancellationToken cancellationToken = default) {

        EventStoreSummaryResult summary = await SummarizeAsync(query, period, cancellationToken)
            .ConfigureAwait(false);
        EventReportRow[] rows = summary.Rows.Select(static row => new EventReportRow {
            TimeCreated = row.PeriodStartUtc,
            Type = "EventStoreSummary",
            Values = new Dictionary<string, object?> {
                [nameof(EventStoreSummaryRow.PeriodStartUtc)] = row.PeriodStartUtc,
                [nameof(EventStoreSummaryRow.DefinitionName)] = row.DefinitionName,
                [nameof(EventStoreSummaryRow.Count)] = row.Count,
                [nameof(EventStoreSummaryRow.FirstEventUtc)] = row.FirstEventUtc,
                [nameof(EventStoreSummaryRow.LastEventUtc)] = row.LastEventUtc
            }
        }).ToArray();
        var schema = new EventReportSectionSchema {
            Name = "EventStoreSummary",
            DisplayName = $"{period} event summary",
            Description = "Stored EventViewerX event counts grouped by UTC calendar period and typed definition.",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                Column(nameof(EventStoreSummaryRow.PeriodStartUtc), "Period start", typeof(DateTime)),
                Column(nameof(EventStoreSummaryRow.DefinitionName), "Event type", typeof(string)),
                Column(nameof(EventStoreSummaryRow.Count), "Events", typeof(long)),
                Column(nameof(EventStoreSummaryRow.FirstEventUtc), "First event", typeof(DateTime)),
                Column(nameof(EventStoreSummaryRow.LastEventUtc), "Latest event", typeof(DateTime))
            }
        };
        return EventReportEngine.CreateStored(
            rows,
            new[] { schema },
            string.IsNullOrWhiteSpace(title) ? $"EventViewerX {period.ToString().ToLowerInvariant()} summary" : title,
            eventsScanned: summary.EventsScanned,
            scanLimitReached: summary.ScanLimitReached);
    }

    /// <summary>Builds hourly, daily, weekly, or monthly summaries without rereading event logs.</summary>
    public async Task<EventStoreSummaryResult> SummarizeAsync(
        EventStoreQuery query,
        EventStoreSummaryPeriod period,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (!Enum.IsDefined(typeof(EventStoreSummaryPeriod), period)) {
            throw new ArgumentOutOfRangeException(nameof(period));
        }
        EventStoreQuery snapshot = query.Snapshot();
        if (snapshot.MaxEvents > 0) {
            throw new ArgumentException(
                "Calendar summaries must be exhaustive. Leave MaxEvents at zero and use MaxCandidates to bound managed predicate evaluation.",
                nameof(query));
        }
        if (snapshot.Predicate != null || RequiresManagedTextMatching(snapshot)) {
            EventReport report = await ReadReportAsync(snapshot, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            EventStoreSummaryRow[] managed = report.Rows
                .GroupBy(row => new SummaryKey(GetPeriodStart(row.TimeCreated, period), row.Type))
                .Select(static group => new EventStoreSummaryRow {
                    PeriodStartUtc = group.Key.PeriodStartUtc,
                    DefinitionName = group.Key.DefinitionName,
                    Count = group.LongCount(),
                    FirstEventUtc = group.Min(static row => row.TimeCreated).ToUniversalTime(),
                    LastEventUtc = group.Max(static row => row.TimeCreated).ToUniversalTime()
                })
                .OrderBy(static row => row.PeriodStartUtc)
                .ThenBy(static row => row.DefinitionName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new EventStoreSummaryResult(managed, report.EventsScanned, report.ScanLimitReached);
        }

        EnsureInitialized();
        WhereCommand filter = BuildWhere(snapshot, includePredicateNative: false);
        string bucket = period switch {
            EventStoreSummaryPeriod.Hour => "strftime('%Y-%m-%dT%H:00:00Z', event_time_utc)",
            EventStoreSummaryPeriod.Day => "strftime('%Y-%m-%dT00:00:00Z', event_time_utc)",
            EventStoreSummaryPeriod.Week =>
                "strftime('%Y-%m-%dT00:00:00Z', event_time_utc, '-' || ((CAST(strftime('%w', event_time_utc) AS INTEGER) + 6) % 7) || ' days')",
            EventStoreSummaryPeriod.Month => "strftime('%Y-%m-01T00:00:00Z', event_time_utc)",
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        string sql = $@"SELECT {bucket} AS period_start, definition_name, COUNT(*),
MIN(event_time_utc), MAX(event_time_utc)
FROM evx_events";
        if (filter.Clauses.Count > 0) {
            sql += " WHERE " + string.Join(" AND ", filter.Clauses);
        }
        sql += " GROUP BY period_start, definition_name ORDER BY period_start, definition_name;";
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<EventStoreSummaryRow> rows = await session.QueryAsListAsync(
            sql,
            static record => new EventStoreSummaryRow {
                PeriodStartUtc = ParseUtc(record.GetString(0)),
                DefinitionName = record.GetString(1),
                Count = record.GetInt64(2),
                FirstEventUtc = ParseUtc(record.GetString(3)),
                LastEventUtc = ParseUtc(record.GetString(4))
            },
            filter.Parameters,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new EventStoreSummaryResult(rows, rows.Sum(static row => row.Count), scanLimitReached: false);
    }

    /// <summary>Removes stored events older than a UTC boundary while retaining definitions and checkpoints.</summary>
    public async Task<int> PruneBeforeAsync(
        DateTime before,
        IReadOnlyList<string>? definitionNames = null,
        CancellationToken cancellationToken = default) {

        EnsureInitialized();
        string[]? selectedDefinitions = EventStoreQuery.NormalizeTextValues(definitionNames);
        if (definitionNames != null && selectedDefinitions == null) {
            return 0;
        }
        var where = new List<string> { "event_time_utc < $before" };
        var parameters = new Dictionary<string, object?> {
            ["$before"] = before.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        if (selectedDefinitions != null && !CanUseSqliteNoCase(selectedDefinitions)) {
            return await session.RunInTransactionAsync(async (transaction, token) => {
                const int pageSize = 500;
                int deleted = 0;
                long afterRowId = 0;
                while (true) {
                    var pageParameters = new Dictionary<string, object?>(parameters) {
                        ["$afterRowId"] = afterRowId,
                        ["$pageSize"] = pageSize
                    };
                    IReadOnlyList<StoredPruneCandidate> candidates = await transaction.QueryAsListAsync(
                        "SELECT rowid, definition_name FROM evx_events " +
                        "WHERE event_time_utc < $before AND rowid > $afterRowId " +
                        "ORDER BY rowid LIMIT $pageSize;",
                        static record => new StoredPruneCandidate(record.GetInt64(0), record.GetString(1)),
                        pageParameters,
                        cancellationToken: token).ConfigureAwait(false);
                    if (candidates.Count == 0) {
                        break;
                    }
                    afterRowId = candidates[candidates.Count - 1].RowId;
                    long[] batch = candidates
                        .Where(candidate => selectedDefinitions.Contains(
                            candidate.DefinitionName,
                            StringComparer.OrdinalIgnoreCase))
                        .Select(static candidate => candidate.RowId)
                        .ToArray();
                    if (batch.Length == 0) {
                        if (candidates.Count < pageSize) {
                            break;
                        }
                        continue;
                    }
                    var deleteParameters = new Dictionary<string, object?>();
                    string[] names = new string[batch.Length];
                    for (int index = 0; index < batch.Length; index++) {
                        names[index] = "$rowId" + index.ToString(CultureInfo.InvariantCulture);
                        deleteParameters[names[index]] = batch[index];
                    }
                    deleted += await transaction.ExecuteNonQueryAsync(
                        "DELETE FROM evx_events WHERE rowid IN (" + string.Join(", ", names) + ");",
                        deleteParameters,
                        token).ConfigureAwait(false);
                    if (candidates.Count < pageSize) {
                        break;
                    }
                }
                return deleted;
            }, cancellationToken).ConfigureAwait(false);
        }
        AddIn(where, parameters, "definition_name", "pruneDefinition", selectedDefinitions, caseInsensitive: true);
        return await session.ExecuteNonQueryAsync(
            "DELETE FROM evx_events WHERE " + string.Join(" AND ", where) + ";",
            parameters,
            cancellationToken).ConfigureAwait(false);
    }

    private static DateTime GetPeriodStart(DateTime value, EventStoreSummaryPeriod period) {
        DateTime utc = value.ToUniversalTime();
        return period switch {
            EventStoreSummaryPeriod.Hour => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
            EventStoreSummaryPeriod.Day => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
            EventStoreSummaryPeriod.Week => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(-(((int)utc.DayOfWeek + 6) % 7)),
            EventStoreSummaryPeriod.Month => new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    private static EventReportColumnSchema Column(string name, string displayName, Type type) => new() {
        Name = name,
        DisplayName = displayName,
        ValueTypeName = EventReportColumnSchema.GetStableTypeName(type)
    };

    private sealed class SummaryKey : IEquatable<SummaryKey> {
        internal SummaryKey(DateTime periodStartUtc, string definitionName) {
            PeriodStartUtc = periodStartUtc;
            DefinitionName = definitionName;
        }

        internal DateTime PeriodStartUtc { get; }
        internal string DefinitionName { get; }

        public bool Equals(SummaryKey? other) => other != null &&
            PeriodStartUtc == other.PeriodStartUtc &&
            string.Equals(DefinitionName, other.DefinitionName, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => Equals(obj as SummaryKey);

        public override int GetHashCode() {
            unchecked {
                return (PeriodStartUtc.GetHashCode() * 397) ^
                       StringComparer.OrdinalIgnoreCase.GetHashCode(DefinitionName);
            }
        }
    }

    private sealed class StoredPruneCandidate {
        internal StoredPruneCandidate(long rowId, string definitionName) {
            RowId = rowId;
            DefinitionName = definitionName;
        }

        internal long RowId { get; }
        internal string DefinitionName { get; }
    }
}
