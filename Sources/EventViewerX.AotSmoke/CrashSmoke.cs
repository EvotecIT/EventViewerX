using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using EventViewerX;
using EventViewerX.Reporting;
using EventViewerX.Rules.Windows;

internal static class CrashSmoke {
    internal static async Task RunAsync() {
        string path = Path.GetTempFileName();
        try {
            var query = new EventTypeQuery(new[] {
                EventType.OSCrash, EventType.OSBugCheck, EventType.OSUncleanShutdown
            }) {
                Paths = new[] { path },
                SavedEventReader = new FixtureReader(),
                Predicate = EventPredicate.AnyOf(
                    EventPredicate.Compare("BugCheckCode", EventPredicateOperator.Equal, "0x0000000a"),
                    EventPredicate.Compare("EventId", EventPredicateOperator.In, 6008, 41)),
                Oldest = true
            };
            var records = new List<EventTypeRecord>();
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query)) {
                records.Add(record);
            }
            if (records.Count != 3) {
                throw new InvalidOperationException($"Crash query returned {records.Count} records instead of three.");
            }

            OSCrash crash = records.OfType<OSCrash>().Single();
            DateTime shutdown = new DateTime(2026, 11, 5, 10, 6, 28, DateTimeKind.Local).ToUniversalTime();
            if (crash.ActionTimestampUtc != shutdown || crash.When != shutdown) {
                throw new InvalidOperationException("The crash projection lost the shutdown timestamp.");
            }
            OSBugCheck bugcheck = records.OfType<OSBugCheck>().Single();
            if (bugcheck.BugCheckCode != "0x0000000a" ||
                bugcheck.Parameter1 != "0xffff800000000001" ||
                bugcheck.Parameter2 != "0x0000000000000002" ||
                bugcheck.Parameter3 != "0x0000000000000000" ||
                bugcheck.Parameter4 != "0xffff800000000004" ||
                bugcheck.DumpFile != @"C:\Windows\MEMORY.DMP" ||
                bugcheck.ReportId != "11111111-2222-3333-4444-555555555555") {
                throw new InvalidOperationException("The bugcheck projection lost its packed WER fields.");
            }
            if (records.OfType<OSUncleanShutdown>().Single().ActionTimestampUtc == null) {
                throw new InvalidOperationException("The unclean shutdown projection lost its timestamp.");
            }

            EventPredicate predicate = EventPredicate.ParseJson(
                EventPredicate.Compare("DumpFile", EventPredicateOperator.EndsWith, "MEMORY.DMP").ToJson());
            Func<EventTypeRecord, bool> match = EventPredicateEvaluator.Compile(predicate);
            if (!match(bugcheck) || match(crash)) {
                throw new InvalidOperationException("The managed predicate did not retain typed record fields.");
            }
            EventObservation observation = EventObservation.Create(bugcheck.SourceEvent, bugcheck);
            using JsonDocument json = JsonDocument.Parse(EventAnalysisJson.Serialize(observation, indented: false, valueTypes: Array.Empty<System.Text.Json.Serialization.Metadata.JsonTypeInfo>()));
            if (json.RootElement.GetProperty("fields").GetProperty("BugCheckCode").GetString() != "0x0000000a") {
                throw new InvalidOperationException("The observation JSON lost typed bugcheck fields.");
            }
            EventReport report = EventReportEngine.Create(records);
            if (report.Rows.Count != 3 || report.Sections.Count == 0) {
                throw new InvalidOperationException("The report projection lost crash rows or columns.");
            }
            await CompanionSmoke.RunAsync(report, observation);

            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(new EventTypeQuery(new[] {
                EventType.OSCrash, EventType.OSBugCheck, EventType.OSUncleanShutdown
            }) {
                MaxEvents = 1,
                Predicate = EventPredicate.Compare("EventId", EventPredicateOperator.GreaterThan, 0)
            })) {
                if (record.EventId <= 0) {
                    throw new InvalidOperationException("The native System-log query returned an invalid event.");
                }
            }
            Console.WriteLine("NativeAOT crash queries, predicates, observations, JSON and report projections passed.");
        } finally {
            File.Delete(path);
        }
    }

    private sealed class FixtureReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {
            string[] events = {
                Event(6008, "EventLog", "<Data>\u200e10:06:28 AM\u200f</Data><Data>\u200e11/\u200e5/\u200e2026</Data>"),
                Event(1001, "Microsoft-Windows-WER-SystemErrorReporting", """
                    <Data Name="param1">0x0000000a (0xffff800000000001, 0x0000000000000002, 0x0000000000000000, 0xffff800000000004)</Data>
                    <Data Name="param2">C:\Windows\MEMORY.DMP</Data>
                    <Data Name="param3">11111111-2222-3333-4444-555555555555</Data>
                    """),
                Event(41, "Microsoft-Windows-Kernel-Power", "<Data Name=\"BugcheckCode\">10</Data>")
            };
            foreach (string xml in events) {
                cancellationToken.ThrowIfCancellationRequested();
                // Windows Event Log XPath uses unqualified element names.
                XElement candidate = XElement.Parse(xml);
                foreach (XElement element in candidate.DescendantsAndSelf()) {
                    element.Name = element.Name.LocalName;
                }
                var container = new XElement("Events", candidate);
                if ((bool)container.XPathEvaluate("boolean(" + query.XPath + ")")) {
                    yield return SavedEventXmlProjector.Create(xml);
                }
            }
        }

        private static string Event(int id, string provider, string data) => $$"""
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><Provider Name="{{provider}}"/><EventID>{{id}}</EventID>
                <TimeCreated SystemTime="2026-11-05T09:09:31Z"/><EventRecordID>{{id}}</EventRecordID>
                <Channel>System</Channel><Computer>aot-fixture</Computer></System>
              <EventData>{{data}}</EventData>
            </Event>
            """;
    }
}
