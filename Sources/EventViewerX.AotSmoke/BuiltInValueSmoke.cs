using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EventViewerX;
using EventViewerX.Reporting;
using EventViewerX.Rules.ActiveDirectory;
using EventViewerX.Rules.Windows;
using EventViewerX.Storage;

internal static class BuiltInValueSmoke {
    internal static async Task RunAsync(string directory) {
        string path = Path.Combine(directory, "built-in-fixture.evtx");
        File.WriteAllBytes(path, Array.Empty<byte>());
        var records = new List<EventTypeRecord>();
        await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(new EventTypeQuery(new[] {
            EventType.ADGroupPolicyEdits, EventType.ADGroupPolicyLinks, EventType.SyncCompleted
        }) { Paths = new[] { path }, SavedEventReader = new Reader() })) {
            records.Add(record);
        }
        if (records.Count != 3) {
            throw new InvalidOperationException("The built-in value query lost its rule projections.");
        }
        foreach (EventTypeRecord record in records) {
            using JsonDocument json = JsonDocument.Parse(EventAnalysisJson.Serialize(
                EventObservation.Create(record.SourceEvent, record), false, Array.Empty<JsonTypeInfo>()));
            JsonElement fields = json.RootElement.GetProperty("fields");
            bool valid = record switch {
                ADGroupPolicyEdits => fields.GetProperty("GroupPolicy").GetProperty("gpoId").GetString() == PolicyId,
                ADGroupPolicyLinks => fields.GetProperty("GroupPolicyLink")[0].GetProperty("guid").GetString() == PolicyId,
                SyncCompleted => fields.GetProperty("Statistics").GetProperty("Added").GetInt32() == 7,
                _ => false
            };
            if (!valid) {
                throw new InvalidOperationException("The native observation lost its built-in structured field.");
            }
        }
        EventReport report = EventReportEngine.Create(records);
        using var evidence = new MemoryStream();
        EventEvidenceBundle.Write(report, evidence, null, CancellationToken.None, Array.Empty<JsonTypeInfo>());
        evidence.Position = 0;
        EventEvidenceBundle.Verify(evidence);
        var store = new EventStore(Path.Combine(directory, "built-in.db"), Array.Empty<JsonTypeInfo>());
        await store.WriteAsync(report);
        EventReport restored = await store.ReadReportAsync(new EventStoreQuery());
        if (restored.Rows.Count != 3 ||
            restored.Rows.Single(static row => row.Type == nameof(EventType.ADGroupPolicyEdits)).Values["GroupPolicy"]
                is not GroupPolicy { GpoId: PolicyId } ||
            restored.Rows.Single(static row => row.Type == nameof(EventType.ADGroupPolicyLinks)).Values["GroupPolicyLink"]
                is not List<GroupPolicyLinks> links || links.Single().Guid != PolicyId ||
            restored.Rows.Single(static row => row.Type == nameof(EventType.SyncCompleted)).Values["Statistics"]
                is not Dictionary<string, int> statistics || statistics["Added"] != 7) {
            throw new InvalidOperationException("The native store lost a built-in structured field or CLR type.");
        }
    }

    private const string PolicyId = "11111111-2222-3333-4444-555555555555";

    private sealed class Reader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {
            yield return Create(5136, 1, "Security", "Microsoft-Windows-Security-Auditing", $$"""
                <Data Name="ObjectClass">groupPolicyContainer</Data>
                <Data Name="AttributeLDAPDisplayName">versionNumber</Data>
                <Data Name="ObjectDN">CN={{{PolicyId}}},CN=Policies,DC=example,DC=test</Data>
                """);
            yield return Create(5136, 2, "Security", "Microsoft-Windows-Security-Auditing", $$"""
                <Data Name="ObjectClass">organizationalUnit</Data>
                <Data Name="AttributeLDAPDisplayName">gpLink</Data>
                <Data Name="OperationType">%%14674</Data>
                <Data Name="AttributeValue">[LDAP://cn={{{PolicyId}}},cn=policies,dc=example,dc=test;0]</Data>
                """);
            SavedEventRecord sync = Create(907, 3, "Application", "Directory Synchronization", "");
            sync.Message = "Added: 7";
            yield return sync;
        }

        private static SavedEventRecord Create(int id, long recordId, string channel, string provider, string data) =>
            SavedEventXmlProjector.Create($$"""
                <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
                  <System><Provider Name="{{provider}}"/><EventID>{{id}}</EventID>
                    <TimeCreated SystemTime="2026-11-05T09:09:31Z"/><EventRecordID>{{recordId}}</EventRecordID>
                    <Channel>{{channel}}</Channel><Computer>aot-fixture</Computer></System>
                  <EventData>{{data}}</EventData>
                </Event>
                """);
    }
}
