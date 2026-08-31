using DBAClientX;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private static readonly StoreTableContract[] StoreSchemaContracts = {
        Table("evx_store_metadata",
            Column("singleton_id", "INTEGER", true, 1),
            Column("schema_version", "INTEGER", true),
            Column("event_identity_version", "INTEGER", true),
            Column("created_utc", "TEXT", true),
            Column("finding_schema_version", "INTEGER", true)),
        Table("evx_definitions",
            Column("definition_name", "TEXT", true, 1),
            Column("display_name", "TEXT", true),
            Column("description", "TEXT", true),
            Column("kind", "INTEGER", true),
            Column("schema_hash", "TEXT", true),
            Column("schema_json", "TEXT", true),
            Column("updated_utc", "TEXT", true)),
        Table("evx_events",
            Column("event_key", "TEXT", true, 1),
            Column("original_event_key", "TEXT", true),
            Column("transport_kind", "INTEGER", true),
            Column("observation_identity", "TEXT", true),
            Column("definition_name", "TEXT", true),
            Column("event_time_utc", "TEXT", true),
            Column("event_id", "INTEGER", true),
            Column("record_id", "INTEGER", false),
            Column("provider", "TEXT", true),
            Column("source_log", "TEXT", true),
            Column("container_log", "TEXT", true),
            Column("source_computer", "TEXT", true),
            Column("collector_computer", "TEXT", true),
            Column("level", "TEXT", true),
            Column("level_value", "INTEGER", false),
            Column("activity_id", "TEXT", false),
            Column("related_activity_id", "TEXT", false),
            Column("process_id", "INTEGER", false),
            Column("thread_id", "INTEGER", false),
            Column("message", "TEXT", true),
            Column("values_json", "TEXT", true),
            Column("received_time_utc", "TEXT", false),
            Column("processed_time_utc", "TEXT", false),
            Column("inserted_utc", "TEXT", true)),
        Table("evx_checkpoints",
            Column("consumer", "TEXT", true, 1),
            Column("computer", "TEXT", true, 2),
            Column("container", "TEXT", true, 3),
            Column("record_id", "INTEGER", false),
            Column("bookmark_xml", "TEXT", false),
            Column("updated_utc", "TEXT", true)),
        Table("evx_findings",
            Column("finding_key", "TEXT", true, 1),
            Column("rule_id", "TEXT", true),
            Column("rule_version", "TEXT", true),
            Column("pack_id", "TEXT", true),
            Column("pack_version", "TEXT", true),
            Column("source_kind", "TEXT", true),
            Column("source_id", "TEXT", true),
            Column("source_status", "TEXT", true),
            Column("source_hash", "TEXT", true),
            Column("content_license", "TEXT", true),
            Column("title", "TEXT", true),
            Column("severity", "INTEGER", true),
            Column("confidence", "INTEGER", true),
            Column("finding_status", "INTEGER", true),
            Column("start_time_utc", "TEXT", true),
            Column("end_time_utc", "TEXT", true),
            Column("tags_json", "TEXT", true),
            Column("false_positives_json", "TEXT", true),
            Column("references_json", "TEXT", true),
            Column("explanation", "TEXT", true),
            Column("completeness_diagnostic", "TEXT", false),
            Column("coverage_json", "TEXT", true),
            Column("inserted_utc", "TEXT", true)),
        Table("evx_finding_evidence",
            Column("finding_key", "TEXT", true, 1),
            Column("ordinal", "INTEGER", true, 2),
            Column("evidence_identity", "TEXT", true),
            Column("type_name", "TEXT", true),
            Column("event_id", "INTEGER", true),
            Column("record_id", "INTEGER", false),
            Column("provider", "TEXT", true),
            Column("source_log", "TEXT", true),
            Column("container_log", "TEXT", true),
            Column("source_computer", "TEXT", true),
            Column("collector_computer", "TEXT", true),
            Column("event_time_utc", "TEXT", true),
            Column("received_time_utc", "TEXT", true),
            Column("processed_time_utc", "TEXT", true)),
        Table("evx_finding_entities",
            Column("finding_key", "TEXT", true, 1),
            Column("field_name", "TEXT", true, 2),
            Column("field_value", "TEXT", true))
    };

    private static readonly StoreForeignKeyContract[] StoreForeignKeyContracts = {
        new("evx_finding_evidence", "finding_key", "evx_findings", "finding_key", "CASCADE"),
        new("evx_finding_entities", "finding_key", "evx_findings", "finding_key", "CASCADE")
    };

    private async Task ValidateStoreSchemaAsync(
        SQLite sqlite,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken) {

        foreach (StoreTableContract table in StoreSchemaContracts) {
            IReadOnlyList<StoreColumnContract> actualColumns = await sqlite.QueryReadOnlyAsListAsync(
                Path,
                $"PRAGMA table_info({table.TableName});",
                static record => new StoreColumnContract(
                    record.GetString(1),
                    record.GetString(2),
                    record.GetInt32(3) != 0,
                    record.GetInt32(5)),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false);
            ValidateColumns(table, actualColumns, diagnostics);
        }

        foreach (StoreForeignKeyContract expected in StoreForeignKeyContracts) {
            IReadOnlyList<StoreForeignKeyContract> actualForeignKeys = await sqlite.QueryReadOnlyAsListAsync(
                Path,
                $"PRAGMA foreign_key_list({expected.TableName});",
                record => new StoreForeignKeyContract(
                    expected.TableName,
                    record.GetString(3),
                    record.GetString(2),
                    record.GetString(4),
                    record.GetString(6)),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false);
            if (!actualForeignKeys.Contains(expected)) {
                diagnostics.Add(
                    $"Required EventStore foreign key '{expected.TableName}.{expected.FromColumn}' to " +
                    $"'{expected.ReferencedTable}.{expected.ReferencedColumn}' with ON DELETE " +
                    $"{expected.OnDelete} is missing.");
            }
        }

        IReadOnlyList<string> foreignKeyFailures = await sqlite.QueryReadOnlyAsListAsync(
            Path,
            "PRAGMA foreign_key_check;",
            static record => string.Concat(
                record.GetValue(0),
                " row ",
                record.GetValue(1),
                " references ",
                record.GetValue(2)),
            cancellationToken: cancellationToken,
            busyTimeoutMs: 10000).ConfigureAwait(false);
        foreach (string failure in foreignKeyFailures) {
            diagnostics.Add("EventStore foreign-key validation failed: " + failure + ".");
        }
    }

    private static void ValidateColumns(
        StoreTableContract table,
        IReadOnlyList<StoreColumnContract> actualColumns,
        ICollection<string> diagnostics) {

        var actualByName = actualColumns.ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);
        foreach (StoreColumnContract expected in table.Columns) {
            if (!actualByName.TryGetValue(expected.Name, out StoreColumnContract actual)) {
                diagnostics.Add($"Required EventStore column '{table.TableName}.{expected.Name}' is missing.");
                continue;
            }
            if (!string.Equals(actual.Type, expected.Type, StringComparison.OrdinalIgnoreCase) ||
                actual.NotNull != expected.NotNull ||
                actual.PrimaryKeyOrdinal != expected.PrimaryKeyOrdinal) {
                diagnostics.Add(
                    $"EventStore column '{table.TableName}.{expected.Name}' does not match the required " +
                    $"type/nullability/primary-key contract.");
            }
        }
        foreach (StoreColumnContract unexpected in actualColumns.Where(column =>
                     !table.Columns.Any(expected => string.Equals(
                         expected.Name,
                         column.Name,
                         StringComparison.OrdinalIgnoreCase)))) {
            diagnostics.Add($"Unexpected EventStore column '{table.TableName}.{unexpected.Name}' was found.");
        }
    }

    private static StoreTableContract Table(string tableName, params StoreColumnContract[] columns) =>
        new(tableName, columns);

    private static StoreColumnContract Column(
        string name,
        string type,
        bool notNull,
        int primaryKeyOrdinal = 0) => new(name, type, notNull, primaryKeyOrdinal);

    private sealed class StoreTableContract {
        internal StoreTableContract(string tableName, StoreColumnContract[] columns) {
            TableName = tableName;
            Columns = columns;
        }

        internal string TableName { get; }
        internal StoreColumnContract[] Columns { get; }
    }

    private readonly struct StoreColumnContract {
        internal StoreColumnContract(string name, string type, bool notNull, int primaryKeyOrdinal) {
            Name = name;
            Type = type;
            NotNull = notNull;
            PrimaryKeyOrdinal = primaryKeyOrdinal;
        }

        internal string Name { get; }
        internal string Type { get; }
        internal bool NotNull { get; }
        internal int PrimaryKeyOrdinal { get; }
    }

    private readonly struct StoreForeignKeyContract : IEquatable<StoreForeignKeyContract> {
        internal StoreForeignKeyContract(
            string tableName,
            string fromColumn,
            string referencedTable,
            string referencedColumn,
            string onDelete) {

            TableName = tableName;
            FromColumn = fromColumn;
            ReferencedTable = referencedTable;
            ReferencedColumn = referencedColumn;
            OnDelete = onDelete;
        }

        internal string TableName { get; }
        internal string FromColumn { get; }
        internal string ReferencedTable { get; }
        internal string ReferencedColumn { get; }
        internal string OnDelete { get; }

        public bool Equals(StoreForeignKeyContract other) =>
            string.Equals(TableName, other.TableName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(FromColumn, other.FromColumn, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ReferencedTable, other.ReferencedTable, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ReferencedColumn, other.ReferencedColumn, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(OnDelete, other.OnDelete, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is StoreForeignKeyContract other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(
            string.Join("\n", TableName, FromColumn, ReferencedTable, ReferencedColumn, OnDelete));
    }
}
