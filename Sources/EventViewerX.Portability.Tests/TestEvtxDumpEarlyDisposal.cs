using EventViewerX.Evtx;
using Xunit;

namespace EventViewerX.Portability.Tests;

public sealed class TestEvtxDumpEarlyDisposal {
    [UnixTheory]
    [InlineData(1)]
    [InlineData(0)]
    public void KnownParserDiagnosticsSurviveEarlyDisposalAndFullEnumeration(int maximumEvents) {
        if (OperatingSystem.IsWindows()) { return; }
        string root = Path.Combine(Path.GetTempPath(), "evx-dump-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
            string xml = Assert.Single(new EvtxSavedEventReader().Read(new EventLogFileQuery(fixture) { Oldest = true })).RawXml;
            string executable = Path.Combine(root, "fake-evtx-dump");
            File.WriteAllText(executable, "#!/bin/sh\ncat <<'EVX_OUTPUT'\nRecord 1\n<Event><EventData>truncated\nRecord 999\n<?xml version=\"1.0\"?>\n" + xml + "\nEVX_OUTPUT\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var diagnostics = new List<SavedEventReadDiagnostic>();
            EventObject[] records = EventLogEngine.ReadFile(new EventLogFileQuery(fixture) {
                Oldest = true, MaxEvents = maximumEvents, SavedEventReader = new EvtxDumpSavedEventReader(executable),
                SavedEventDiagnosticHandler = diagnostics.Add
            }).ToArray();
            Assert.Single(records);
            Assert.True(Assert.Single(diagnostics, item => item.Code == "EVXEVTX304").AffectsCompleteness);
            Assert.Single(diagnostics, item => item.Code == "EVXEVTX305");
        } finally { Directory.Delete(root, true); }
    }

    private sealed class UnixTheoryAttribute : TheoryAttribute {
        public UnixTheoryAttribute() {
            if (OperatingSystem.IsWindows()) { Skip = "Requires a POSIX executable fixture; exercised by the Linux portability lane."; }
        }
    }
}
