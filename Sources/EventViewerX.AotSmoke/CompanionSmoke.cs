using System.IO.Compression;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;
using EventViewerX;
using EventViewerX.Evtx;
using EventViewerX.Reporting;
using EventViewerX.Sigma;
using EventViewerX.Storage;

internal static class CompanionSmoke {
    internal static async Task RunAsync(EventReport report, EventObservation observation) {
        string directory = Path.Combine(Path.GetTempPath(), "eventviewerx-aot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            CustomValueSmoke.Run(observation.SourceEvent);
            await BuiltInValueSmoke.RunAsync(directory);
            var store = new EventStore(Path.Combine(directory, "events.db"), Array.Empty<JsonTypeInfo>());
            EventStoreWriteResult written = await store.WriteAsync(report);
            EventReport restored = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = EventPredicate.Compare("EventId", EventPredicateOperator.Equal, 1001)
            });
            if (written.Inserted != 3 || restored.Rows.Count != 1 ||
                restored.Rows[0].Values["BugCheckCode"]?.ToString() != "0x0000000a") {
                throw new InvalidOperationException("The native store lost crash rows or typed field values.");
            }

            using var evidence = new MemoryStream();
            EventEvidenceBundle.Write(report, evidence, null, CancellationToken.None, Array.Empty<JsonTypeInfo>());
            evidence.Position = 0;
            EventEvidenceBundleManifest manifest = EventEvidenceBundle.Verify(evidence);
            if (!manifest.Entries.Any(static entry => entry.Name == "rows.jsonl")) {
                throw new InvalidOperationException("The native evidence bundle lost its rows.");
            }
            string csv = EventReportCsvRenderer.Save(report, Path.Combine(directory, "events.zip"));
            using (ZipArchive archive = ZipFile.OpenRead(csv)) {
                if (!archive.Entries.Any(static entry => entry.FullName.EndsWith(".csv", StringComparison.Ordinal))) {
                    throw new InvalidOperationException("The native CSV bundle lost its sections.");
                }
            }
            string html = EventReportHtmlRenderer.Render(report);
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            if (!html.Contains("0x0000000a", StringComparison.Ordinal) ||
                !email.Html.Contains("0x0000000a", StringComparison.Ordinal)) {
                throw new InvalidOperationException("The native report renderers lost typed bugcheck values.");
            }

            const string yaml = """
                title: Native bugcheck
                id: 40c0161d-fc5d-473e-8704-d903b5e04c54
                logsource:
                  product: windows
                  service: system
                detection:
                  selection:
                    EventID: 1001
                  condition: selection
                """;
            EventDetectionPlan plan = SigmaRuleCompiler.CompileYaml(yaml).CompilePlan();
            if (EventDetectionEngine.Stream(new[] { observation }, plan).Count() != 1) {
                throw new InvalidOperationException("The native Sigma plan lost its matching bugcheck.");
            }

            string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
            SavedEventRecord first = new EvtxSavedEventReader().Read(new EventLogFileQuery(fixture) {
                Oldest = true, MaxEvents = 1
            }).First();
            if (first.RecordId != 37 || XDocument.Parse(first.RawXml).Root?.Name.LocalName != "Event") {
                throw new InvalidOperationException("The native EVTX reader lost its record or template XML.");
            }
            Console.WriteLine("NativeAOT storage, evidence, CSV, HTML, email, Sigma and EVTX passed.");
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
