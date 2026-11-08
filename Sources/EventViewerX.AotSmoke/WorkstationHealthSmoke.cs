using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using EventViewerX;
using EventViewerX.Rules.Windows;

internal static class WorkstationHealthSmoke {
    internal static async Task RunAsync() {
        string path = Path.GetTempFileName();
        try {
            var query = new EventTypeQuery(new[] {
                EventType.ApplicationCrash, EventType.ApplicationHang, EventType.ServiceFailure,
                EventType.DiskError, EventType.FileSystemError, EventType.StorageReset,
                EventType.HardwareError, EventType.DisplayDriverReset, EventType.BootPerformance,
                EventType.ShutdownPerformance, EventType.StartupDegradation, EventType.ShutdownDegradation,
                EventType.BitLockerSuspended, EventType.WindowsUpdateFailure
            }) {
                Paths = new[] { path },
                SavedEventReader = new FixtureReader(),
                Predicate = EventPredicate.AnyOf(
                    EventPredicate.Compare("ApplicationName", EventPredicateOperator.Equal, "HealthFixture.exe"),
                    EventPredicate.Compare("EventId", EventPredicateOperator.NotEqual, 1000)),
                Oldest = true
            };
            var records = new List<EventTypeRecord>();
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query)) {
                records.Add(record);
                EventObservation observation = EventObservation.Create(record.SourceEvent, record);
                using JsonDocument json = JsonDocument.Parse(EventAnalysisJson.Serialize(observation,
                    indented: false, valueTypes: Array.Empty<System.Text.Json.Serialization.Metadata.JsonTypeInfo>()));
                if (!json.RootElement.TryGetProperty("fields", out JsonElement fields) || fields.GetRawText() == "{}") {
                    throw new InvalidOperationException("A health observation lost its typed payload.");
                }
            }
            if (records.Count != 15 || records.Select(record => record.TypeName).Distinct().Count() != 14 ||
                records.OfType<BitLockerSuspended>().Single().EventId != 773 ||
                records.OfType<WindowsUpdateFailure>().Single().Reason != "0x80073d02" ||
                records.OfType<ApplicationCrash>().Single().ExceptionCode != "c0000409" ||
                records.OfType<ApplicationHang>().Single().ApplicationName != "FTK Imager.exe" ||
                records.OfType<ServiceFailure>().Single(record => record.EventId == 7031).RecoveryDelayMilliseconds != 180000 ||
                records.OfType<DiskError>().Single().DiskNumber != 1 ||
                records.OfType<FileSystemError>().Any(record => record.EventId == 98) ||
                records.OfType<StorageReset>().Single().DeviceName != @"\Device\RaidPort1" ||
                !records.OfType<HardwareError>().Single().IsCorrected ||
                records.OfType<DisplayDriverReset>().Single().DriverName != "nvlddmkm" ||
                records.OfType<BootPerformance>().Single().MainPathMilliseconds != 33326 ||
                records.OfType<ShutdownPerformance>().Single().DurationMilliseconds != 31312 ||
                records.OfType<StartupDegradation>().Single().DegradationMilliseconds != 1341 ||
                records.OfType<ShutdownDegradation>().Single().Name != "Dhcp") {
                throw new InvalidOperationException("The native health query lost, misclassified or misparsed a captured event.");
            }
            Console.WriteLine("NativeAOT workstation health queries, provider filters, typed fields and observation JSON passed.");
        } finally {
            File.Delete(path);
        }
    }

    private sealed class FixtureReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {
            foreach (string name in typeof(WorkstationHealthSmoke).Assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("WorkstationHealth.", StringComparison.Ordinal))) {
                cancellationToken.ThrowIfCancellationRequested();
                using Stream stream = typeof(WorkstationHealthSmoke).Assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                string xml = reader.ReadToEnd();
                XElement candidate = XElement.Parse(xml);
                foreach (XElement element in candidate.DescendantsAndSelf()) {
                    element.Name = element.Name.LocalName;
                }
                if ((bool)new XElement("Events", candidate).XPathEvaluate("boolean(" + query.XPath + ")")) {
                    yield return SavedEventXmlProjector.Create(xml);
                }
            }
        }
    }
}
