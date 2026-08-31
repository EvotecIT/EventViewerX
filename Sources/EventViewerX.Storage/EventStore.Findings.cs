using System.Security.Cryptography;
using System.Text.Json;
using DBAClientX;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private const int ManagedFindingPageSize = 128;

    /// <summary>Stores immutable finding and evidence snapshots in one idempotent transaction.</summary>
    public async Task<EventFindingStoreWriteResult> WriteFindingsAsync(
        IEnumerable<EventDetectionFinding> findings,
        CancellationToken cancellationToken = default) {

        if (findings == null) {
            throw new ArgumentNullException(nameof(findings));
        }
        EventDetectionFinding[] snapshots = findings.ToArray();
        if (snapshots.Any(static finding => finding == null)) {
            throw new ArgumentException("Findings cannot contain null values.", nameof(findings));
        }
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.RunInTransactionAsync(async (transaction, token) => {
            int inserted = 0;
            string insertedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            foreach (EventDetectionFinding finding in snapshots) {
                token.ThrowIfCancellationRequested();
                string key = CreateFindingKey(finding);
                int findingInserted = await transaction.ExecuteNonQueryAsync(
                    InsertFindingSql,
                    CreateFindingParameters(finding, key, insertedAt),
                    token).ConfigureAwait(false);
                inserted += findingInserted;
                if (findingInserted == 0) {
                    continue;
                }
                for (int index = 0; index < finding.Evidence.Count; index++) {
                    EventObservation evidence = finding.Evidence[index];
                    await transaction.ExecuteNonQueryAsync(
                        InsertFindingEvidenceSql,
                        CreateEvidenceParameters(evidence, key, index),
                        token).ConfigureAwait(false);
                }
                foreach (KeyValuePair<string, string> entity in finding.Entities) {
                    await transaction.ExecuteNonQueryAsync(
                        InsertFindingEntitySql,
                        new Dictionary<string, object?> {
                            ["$findingKey"] = key,
                            ["$field"] = entity.Key,
                            ["$value"] = entity.Value
                        },
                        token).ConfigureAwait(false);
                }
            }
            return new EventFindingStoreWriteResult(snapshots.Length, inserted);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads durable finding snapshots with indexed rule, pack, status, severity, time, and entity selectors.</summary>
    public async Task<IReadOnlyList<StoredEventDetectionFinding>> ReadFindingsAsync(
        EventFindingStoreQuery? query = null,
        CancellationToken cancellationToken = default) {

        EventFindingStoreQuery snapshot = (query ?? new EventFindingStoreQuery()).Snapshot();
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.RunInTransactionAsync(
            (transaction, token) => ReadFindingSnapshotAsync(transaction, snapshot, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<StoredEventDetectionFinding>> ReadFindingSnapshotAsync(
        SQLiteAsyncSession session,
        EventFindingStoreQuery snapshot,
        CancellationToken cancellationToken) {

        var where = new List<string>();
        var parameters = new Dictionary<string, object?>();
        AddBoundary(where, parameters, "f.end_time_utc", "$start", ">=", ToUtcText(snapshot.StartTime));
        AddBoundary(where, parameters, "f.start_time_utc", "$end", "<=", ToUtcText(snapshot.EndTime));
        AddSqliteNoCaseIn(where, parameters, "f.rule_id", "findingRule", snapshot.RuleIds);
        AddSqliteNoCaseIn(where, parameters, "f.pack_id", "findingPack", snapshot.PackIds);
        AddIn(where, parameters, "f.severity", "findingSeverity", snapshot.Severities?.Select(static value => (int)value).ToArray());
        AddIn(where, parameters, "f.finding_status", "findingStatus", snapshot.Statuses?.Select(static value => (int)value).ToArray());
        bool entityCanPush = snapshot.EntityField == null ||
            CanUseSqliteNoCase(new[] { snapshot.EntityField, snapshot.EntityValue! });
        if (snapshot.EntityField != null && entityCanPush) {
            where.Add(
                "EXISTS (SELECT 1 FROM evx_finding_entities e " +
                "WHERE e.finding_key = f.finding_key " +
                "AND e.field_name = $entityField COLLATE NOCASE " +
                "AND e.field_value = $entityValue COLLATE NOCASE)");
            parameters["$entityField"] = snapshot.EntityField;
            parameters["$entityValue"] = snapshot.EntityValue;
        }
        bool managedText = !CanUseSqliteNoCase(snapshot.RuleIds) ||
                           !CanUseSqliteNoCase(snapshot.PackIds) ||
                           !entityCanPush;
        string sql = SelectFindingsSql;
        if (where.Count > 0) {
            sql += " WHERE " + string.Join(" AND ", where);
        }
        sql += snapshot.Oldest
            ? " ORDER BY f.start_time_utc, f.finding_key"
            : " ORDER BY f.start_time_utc DESC, f.finding_key DESC";
        StoredFindingRow[] rows;
        if (snapshot.MaxFindings > 0 && managedText) {
            rows = await ReadManagedFindingRowsAsync(
                session,
                sql,
                parameters,
                snapshot,
                entityCanPush,
                cancellationToken).ConfigureAwait(false);
        } else {
            if (snapshot.MaxFindings > 0) {
                sql += " LIMIT $limit";
                parameters["$limit"] = snapshot.MaxFindings;
            }
            sql += ";";
            IReadOnlyList<StoredFindingRow> candidates = await session.QueryAsListAsync(
                sql,
                MapFindingRow,
                parameters,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            rows = candidates.Where(row =>
                    MatchesText(snapshot.RuleIds, row.RuleId) &&
                    MatchesText(snapshot.PackIds, row.PackId))
                .ToArray();
        }
        return await RestoreFindingRowsAsync(
            session,
            rows,
            snapshot,
            entityCanPush,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<StoredFindingRow[]> ReadManagedFindingRowsAsync(
        SQLiteAsyncSession session,
        string sql,
        IDictionary<string, object?> parameters,
        EventFindingStoreQuery snapshot,
        bool entityCanPush,
        CancellationToken cancellationToken) {

        var rows = new List<StoredFindingRow>();
        long offset = 0;
        while (rows.Count < snapshot.MaxFindings) {
            var pageParameters = new Dictionary<string, object?>(parameters) {
                ["$pageLimit"] = ManagedFindingPageSize,
                ["$pageOffset"] = offset
            };
            IReadOnlyList<StoredFindingRow> page = await session.QueryAsListAsync(
                sql + " LIMIT $pageLimit OFFSET $pageOffset;",
                MapFindingRow,
                pageParameters,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (page.Count == 0) {
                break;
            }
            offset += page.Count;
            StoredFindingRow[] matching = page.Where(row =>
                    MatchesText(snapshot.RuleIds, row.RuleId) &&
                    MatchesText(snapshot.PackIds, row.PackId))
                .ToArray();
            if (snapshot.EntityField != null && !entityCanPush && matching.Length > 0) {
                IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> entities =
                    await ReadFindingEntitiesAsync(
                        session,
                        matching.Select(static row => row.FindingId).ToArray(),
                        cancellationToken).ConfigureAwait(false);
                matching = matching.Where(row =>
                        entities.TryGetValue(row.FindingId, out IReadOnlyDictionary<string, string>? findingEntities) &&
                        findingEntities.TryGetValue(snapshot.EntityField, out string? value) &&
                        string.Equals(value, snapshot.EntityValue, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            foreach (StoredFindingRow row in matching) {
                rows.Add(row);
                if (rows.Count >= snapshot.MaxFindings) {
                    break;
                }
            }
            if (page.Count < ManagedFindingPageSize) {
                break;
            }
        }
        return rows.ToArray();
    }

    private static async Task<IReadOnlyList<StoredEventDetectionFinding>> RestoreFindingRowsAsync(
        SQLiteAsyncSession session,
        StoredFindingRow[] rows,
        EventFindingStoreQuery snapshot,
        bool entityCanPush,
        CancellationToken cancellationToken) {

        if (rows.Length == 0) {
            return Array.Empty<StoredEventDetectionFinding>();
        }
        string[] findingIds = rows.Select(static row => row.FindingId).ToArray();
        var detailParameters = new Dictionary<string, object?> {
            ["$findingIds"] = JsonSerializer.Serialize(findingIds, JsonOptions)
        };
        IReadOnlyList<StoredEvidenceRow> evidenceRows = await session.QueryAsListAsync(
            SelectFindingEvidenceSql,
            MapEvidenceRow,
            detailParameters,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> entitiesByFinding =
            await ReadFindingEntitiesAsync(session, findingIds, cancellationToken).ConfigureAwait(false);
        var evidenceByFinding = evidenceRows.GroupBy(static row => row.FindingId)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<StoredEventDetectionEvidence>)group
                    .OrderBy(static row => row.Ordinal)
                    .Select(static row => row.Evidence)
                    .ToArray(),
                StringComparer.Ordinal);
        IEnumerable<StoredFindingRow> entitySelected = rows;
        if (snapshot.EntityField != null && !entityCanPush) {
            entitySelected = entitySelected.Where(row =>
                entitiesByFinding.TryGetValue(row.FindingId, out IReadOnlyDictionary<string, string>? entities) &&
                entities.TryGetValue(snapshot.EntityField, out string? value) &&
                string.Equals(value, snapshot.EntityValue, StringComparison.OrdinalIgnoreCase));
        }
        return entitySelected.Select(row => row.Create(
                evidenceByFinding.TryGetValue(row.FindingId, out IReadOnlyList<StoredEventDetectionEvidence>? evidence)
                    ? evidence
                    : Array.Empty<StoredEventDetectionEvidence>(),
                entitiesByFinding.TryGetValue(row.FindingId, out IReadOnlyDictionary<string, string>? entities)
                    ? entities
                    : new Dictionary<string, string>()))
            .Take(snapshot.MaxFindings > 0 ? snapshot.MaxFindings : int.MaxValue)
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ReadFindingEntitiesAsync(
        SQLiteAsyncSession session,
        string[] findingIds,
        CancellationToken cancellationToken) {

        if (findingIds.Length == 0) {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        }
        var parameters = new Dictionary<string, object?> {
            ["$findingIds"] = JsonSerializer.Serialize(findingIds, JsonOptions)
        };
        IReadOnlyList<StoredEntityRow> entityRows = await session.QueryAsListAsync(
            SelectFindingEntitiesSql,
            static record => new StoredEntityRow(record.GetString(0), record.GetString(1), record.GetString(2)),
            parameters,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return entityRows.GroupBy(static row => row.FindingId)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyDictionary<string, string>)group.ToDictionary(
                    static row => row.Field,
                    static row => row.Value,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> CreateFindingParameters(
        EventDetectionFinding finding,
        string findingKey,
        string insertedAt) => new() {
            ["$findingKey"] = findingKey,
            ["$ruleId"] = finding.RuleId,
            ["$ruleVersion"] = finding.RuleVersion,
            ["$packId"] = finding.PackId,
            ["$packVersion"] = finding.PackVersion,
            ["$sourceKind"] = finding.SourceKind,
            ["$sourceId"] = finding.SourceId,
            ["$sourceStatus"] = finding.SourceStatus,
            ["$sourceHash"] = finding.SourceHash,
            ["$license"] = finding.License,
            ["$title"] = finding.Title,
            ["$severity"] = (int)finding.Severity,
            ["$confidence"] = finding.Confidence,
            ["$status"] = (int)finding.Status,
            ["$start"] = finding.StartTimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["$end"] = finding.EndTimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["$tags"] = JsonSerializer.Serialize(finding.Tags, JsonOptions),
            ["$falsePositives"] = JsonSerializer.Serialize(finding.FalsePositives, JsonOptions),
            ["$references"] = JsonSerializer.Serialize(finding.References, JsonOptions),
            ["$explanation"] = finding.Explanation,
            ["$diagnostic"] = finding.CompletenessDiagnostic,
            ["$coverage"] = finding.Coverage.ToJson(),
            ["$inserted"] = insertedAt
        };

    private static Dictionary<string, object?> CreateEvidenceParameters(
        EventObservation evidence,
        string findingKey,
        int ordinal) => new() {
            ["$findingKey"] = findingKey,
            ["$ordinal"] = ordinal,
            ["$identity"] = evidence.Identity,
            ["$type"] = evidence.TypeName,
            ["$eventId"] = evidence.EventId,
            ["$recordId"] = evidence.RecordId,
            ["$provider"] = evidence.ProviderName,
            ["$sourceLog"] = evidence.SourceLog,
            ["$containerLog"] = evidence.ContainerLog,
            ["$sourceComputer"] = evidence.SourceComputer,
            ["$collectorComputer"] = evidence.CollectorComputer,
            ["$eventTime"] = evidence.EventTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            ["$receivedTime"] = evidence.ReceivedTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            ["$processedTime"] = evidence.ProcessedTimeUtc.ToString("O", CultureInfo.InvariantCulture)
        };

    private static string CreateFindingKey(EventDetectionFinding finding) {
        var canonical = new StringBuilder();
        AppendFindingKeyPart(canonical, finding.RuleId);
        AppendFindingKeyPart(canonical, finding.RuleVersion);
        AppendFindingKeyPart(canonical, finding.PackId);
        AppendFindingKeyPart(canonical, finding.PackVersion);
        AppendFindingKeyPart(canonical, finding.SourceHash);
        AppendFindingKeyPart(canonical, ((int)finding.Status).ToString(CultureInfo.InvariantCulture));
        AppendFindingKeyPart(canonical, finding.StartTimeUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        AppendFindingKeyPart(canonical, finding.EndTimeUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        AppendFindingKeyPart(canonical, finding.Coverage.ToJson());
        if (finding.Status != EventDetectionFindingStatus.Matched) {
            AppendFindingKeyPart(canonical, finding.CompletenessDiagnostic ?? string.Empty);
            AppendFindingKeyPart(canonical, finding.Explanation);
        }
        foreach (string identity in finding.EvidenceIdentities) {
            AppendFindingKeyPart(canonical, identity);
        }
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static void AppendFindingKeyPart(StringBuilder builder, string value) {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
    }

    private static StoredFindingRow MapFindingRow(IDataRecord record) => new(
        record.GetString(0),
        record.GetString(1),
        record.GetString(2),
        record.GetString(3),
        record.GetString(4),
        record.GetString(5),
        record.GetString(6),
        record.GetString(7),
        record.GetString(8),
        record.GetString(9),
        record.GetString(10),
        (EventDetectionSeverity)record.GetInt32(11),
        record.GetInt32(12),
        (EventDetectionFindingStatus)record.GetInt32(13),
        ParseUtc(record.GetString(14)),
        ParseUtc(record.GetString(15)),
        DeserializeStringArray(record.GetString(16)),
        DeserializeStringArray(record.GetString(17)),
        DeserializeStringArray(record.GetString(18)),
        record.GetString(19),
        record.IsDBNull(20) ? null : record.GetString(20),
        DeserializeCoverage(record.GetString(21)),
        ParseUtc(record.GetString(22)));

    private static StoredEvidenceRow MapEvidenceRow(IDataRecord record) => new(
        record.GetString(0),
        record.GetInt32(1),
        new StoredEventDetectionEvidence(
            record.GetString(2),
            record.GetString(3),
            record.GetInt32(4),
            record.IsDBNull(5) ? null : record.GetInt64(5),
            record.GetString(6),
            record.GetString(7),
            record.GetString(8),
            record.GetString(9),
            record.GetString(10),
            ParseUtc(record.GetString(11)),
            ParseUtc(record.GetString(12)),
            ParseUtc(record.GetString(13))));

    private static string[] DeserializeStringArray(string json) =>
        JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();

    private static EventDetectionCoverage DeserializeCoverage(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? EventDetectionCoverage.Unknown()
            : EventDetectionCoverage.FromJson(json);

    private const string InsertFindingSql = @"
INSERT OR IGNORE INTO evx_findings (
    finding_key, rule_id, rule_version, pack_id, pack_version,
    source_kind, source_id, source_status, source_hash, content_license,
    title, severity, confidence, finding_status, start_time_utc, end_time_utc,
    tags_json, false_positives_json, references_json, explanation,
    completeness_diagnostic, coverage_json, inserted_utc)
VALUES (
    $findingKey, $ruleId, $ruleVersion, $packId, $packVersion,
    $sourceKind, $sourceId, $sourceStatus, $sourceHash, $license,
    $title, $severity, $confidence, $status, $start, $end,
    $tags, $falsePositives, $references, $explanation, $diagnostic, $coverage, $inserted);";

    private const string InsertFindingEvidenceSql = @"
INSERT INTO evx_finding_evidence (
    finding_key, ordinal, evidence_identity, type_name, event_id, record_id,
    provider, source_log, container_log, source_computer, collector_computer,
    event_time_utc, received_time_utc, processed_time_utc)
VALUES (
    $findingKey, $ordinal, $identity, $type, $eventId, $recordId,
    $provider, $sourceLog, $containerLog, $sourceComputer, $collectorComputer,
    $eventTime, $receivedTime, $processedTime);";

    private const string InsertFindingEntitySql = @"
INSERT INTO evx_finding_entities (finding_key, field_name, field_value)
VALUES ($findingKey, $field, $value);";

    private const string SelectFindingsSql = @"
SELECT f.finding_key, f.rule_id, f.rule_version, f.pack_id, f.pack_version,
       f.source_kind, f.source_id, f.source_status, f.source_hash, f.content_license,
       f.title, f.severity, f.confidence, f.finding_status,
       f.start_time_utc, f.end_time_utc, f.tags_json, f.false_positives_json,
       f.references_json, f.explanation, f.completeness_diagnostic, f.coverage_json, f.inserted_utc
FROM evx_findings f";

    private const string SelectFindingEvidenceSql = @"
SELECT finding_key, ordinal, evidence_identity, type_name, event_id, record_id,
       provider, source_log, container_log, source_computer, collector_computer,
       event_time_utc, received_time_utc, processed_time_utc
FROM evx_finding_evidence
WHERE finding_key IN (SELECT value FROM json_each($findingIds))
ORDER BY finding_key, ordinal;";

    private const string SelectFindingEntitiesSql = @"
SELECT finding_key, field_name, field_value
FROM evx_finding_entities
WHERE finding_key IN (SELECT value FROM json_each($findingIds));";

    private sealed class StoredFindingRow {
        internal StoredFindingRow(
            string findingId, string ruleId, string ruleVersion, string packId, string packVersion,
            string sourceKind, string sourceId, string sourceStatus, string sourceHash, string license,
            string title, EventDetectionSeverity severity, int confidence, EventDetectionFindingStatus status,
            DateTime startTimeUtc, DateTime endTimeUtc, IReadOnlyList<string> tags,
            IReadOnlyList<string> falsePositives, IReadOnlyList<string> references, string explanation,
            string? completenessDiagnostic, EventDetectionCoverage coverage, DateTime insertedTimeUtc) {

            FindingId = findingId;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            PackId = packId;
            PackVersion = packVersion;
            SourceKind = sourceKind;
            SourceId = sourceId;
            SourceStatus = sourceStatus;
            SourceHash = sourceHash;
            License = license;
            Title = title;
            Severity = severity;
            Confidence = confidence;
            Status = status;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            Tags = tags;
            FalsePositives = falsePositives;
            References = references;
            Explanation = explanation;
            CompletenessDiagnostic = completenessDiagnostic;
            Coverage = coverage;
            InsertedTimeUtc = insertedTimeUtc;
        }

        internal string FindingId { get; }
        internal string RuleId { get; }
        internal string RuleVersion { get; }
        internal string PackId { get; }
        internal string PackVersion { get; }
        internal string SourceKind { get; }
        internal string SourceId { get; }
        internal string SourceStatus { get; }
        internal string SourceHash { get; }
        internal string License { get; }
        internal string Title { get; }
        internal EventDetectionSeverity Severity { get; }
        internal int Confidence { get; }
        internal EventDetectionFindingStatus Status { get; }
        internal DateTime StartTimeUtc { get; }
        internal DateTime EndTimeUtc { get; }
        internal IReadOnlyList<string> Tags { get; }
        internal IReadOnlyList<string> FalsePositives { get; }
        internal IReadOnlyList<string> References { get; }
        internal string Explanation { get; }
        internal string? CompletenessDiagnostic { get; }
        internal EventDetectionCoverage Coverage { get; }
        internal DateTime InsertedTimeUtc { get; }

        internal StoredEventDetectionFinding Create(
            IReadOnlyList<StoredEventDetectionEvidence> evidence,
            IReadOnlyDictionary<string, string> entities) => new(
                FindingId, RuleId, RuleVersion, PackId, PackVersion, SourceKind, SourceId,
                SourceStatus, SourceHash, License, Title, Severity, Confidence, Status,
                StartTimeUtc, EndTimeUtc, Tags, FalsePositives, References, entities, evidence,
                Coverage, Explanation, CompletenessDiagnostic, InsertedTimeUtc);
    }

    private sealed class StoredEvidenceRow {
        internal StoredEvidenceRow(string findingId, int ordinal, StoredEventDetectionEvidence evidence) {
            FindingId = findingId;
            Ordinal = ordinal;
            Evidence = evidence;
        }

        internal string FindingId { get; }
        internal int Ordinal { get; }
        internal StoredEventDetectionEvidence Evidence { get; }
    }

    private sealed class StoredEntityRow {
        internal StoredEntityRow(string findingId, string field, string value) {
            FindingId = findingId;
            Field = field;
            Value = value;
        }

        internal string FindingId { get; }
        internal string Field { get; }
        internal string Value { get; }
    }
}
