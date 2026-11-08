using EventViewerX;

internal static class CustomSourceSmoke {
    internal static async Task RunAsync() {
        EventTypeCatalog.RegisterBuiltInRules();
        // Synthetic source metadata proves host registration precedence, not SQL Audit ingestion.
        EventTypeCatalog.RegisterRuleFactory(EventType.SqlDatabaseCreated, "Application", new[] { 9001 },
            new[] { "AuditFixture" }, static source => new ReplacementRecord(source), canHandle: null);
        EventTypeCatalog.Configure(EventRuleDiscoveryMode.ExplicitOnly);
        EventSourceDefinition selected = EventTypeCatalog.GetSources(new[] { EventType.SqlDatabaseCreated }).Single();
        if (!selected.EventIds.SequenceEqual(new[] { 9001 }) || !selected.ProviderNames.SequenceEqual(new[] { "AuditFixture" })) {
            throw new InvalidOperationException("The custom native source was not selected.");
        }
        string query = EventDefinitionCompiler.BuildQueryXml(new[] { EventType.SqlDatabaseCreated });
        if (!query.Contains("AuditFixture", StringComparison.Ordinal) || query.Contains("17137", StringComparison.Ordinal)) {
            throw new InvalidOperationException("The custom native source was not compiled.");
        }
        string path = Path.GetTempFileName();
        try {
            var queryRequest = new EventTypeQuery(new[] { EventType.SqlDatabaseCreated }) {
                Paths = new[] { path }, SavedEventReader = new ReplacementReader(), ReadMode = EventReadMode.StructuredData
            };
            var records = new List<EventTypeRecord>();
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(queryRequest)) { records.Add(record); }
            if (records.Count != 1 || records[0] is not ReplacementRecord || records[0].EventId != 9001) {
                throw new InvalidOperationException("The custom projector was not selected.");
            }
        } finally {
            File.Delete(path);
        }
        if (EventTypeCatalog.GetDefinition(EventType.SqlDatabaseCreated).UnavailableReason != null) {
            throw new InvalidOperationException("Custom source metadata retained the built-in unavailability reason.");
        }
        Console.WriteLine("NativeAOT custom source registration, metadata, query and projection passed.");
    }

    private sealed class ReplacementRecord : EventTypeRecord {
        internal ReplacementRecord(EventObject source) : base(source) { TypeName = nameof(EventType.SqlDatabaseCreated); }
    }

    private sealed class ReplacementReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SavedEventRecord {
                EventId = 9001, Channel = "Application", ProviderName = "AuditFixture", RecordId = 1,
                TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)
            };
        }
    }
}