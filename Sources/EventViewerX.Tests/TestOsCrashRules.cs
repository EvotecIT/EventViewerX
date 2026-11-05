using EventViewerX.Native;
using EventViewerX.Rules.Windows;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestOsCrashRules {
    [Theory]
    [InlineData(0x200e)]
    [InlineData(0x200f)]
    [InlineData(0x061c)]
    [InlineData(0x2066)]
    public void OSCrash_UsesShutdownTimeWithFormattingMarks(int markCode) {
        char mark = (char)markCode;
        EventObject source = CreateEvent(6008, "EventLog", $"""
            <Data>{mark}10:06:28 AM{mark}</Data>
            <Data>{mark}11/{mark}5/{mark}2026</Data>
            """);

        var crash = new OSCrash(source);
        DateTime expected = new DateTime(2026, 11, 5, 10, 6, 28, DateTimeKind.Local).ToUniversalTime();

        Assert.True(crash.CanHandle(source));
        Assert.Equal(expected, crash.ActionTimestampUtc);
        Assert.Equal(DateTimeKind.Utc, crash.ActionTimestampUtc!.Value.Kind);
        Assert.Equal(expected, crash.When);
        Assert.NotEqual(source.TimeCreated, crash.When);
    }

    [Theory]
    [InlineData("wer")]
    [InlineData("named")]
    [InlineData("positional")]
    public void OSBugCheck_MapsCodeParametersAndReportFields(string layout) {
        string data = layout switch {
            "wer" => """
                <Data Name="param1">0x0000000a (0xffff800000000001, 0x0000000000000002, 0x0000000000000000, 0xffff800000000004)</Data>
                <Data Name="param2">C:\Windows\MEMORY.DMP</Data>
                <Data Name="param3">11111111-2222-3333-4444-555555555555</Data>
                """,
            "named" => """
                <Data Name="BugcheckCode">0x0000000a</Data>
                <Data Name="BugcheckParameter1">0xffff800000000001</Data>
                <Data Name="BugcheckParameter2">0x0000000000000002</Data>
                <Data Name="BugcheckParameter3">0x0000000000000000</Data>
                <Data Name="BugcheckParameter4">0xffff800000000004</Data>
                <Data Name="DumpFile">C:\Windows\MEMORY.DMP</Data>
                <Data Name="ReportId">11111111-2222-3333-4444-555555555555</Data>
                """,
            _ => """
                <Data Name="param1">0x0000000a</Data>
                <Data Name="param2">0xffff800000000001</Data>
                <Data Name="param3">0x0000000000000002</Data>
                <Data Name="param4">0x0000000000000000</Data>
                <Data Name="param5">0xffff800000000004</Data>
                <Data Name="DumpFile">C:\Windows\MEMORY.DMP</Data>
                <Data Name="ReportId">11111111-2222-3333-4444-555555555555</Data>
                """
        };
        EventObject source = CreateEvent(1001,
            layout == "positional" ? "BugCheck" : "Microsoft-Windows-WER-SystemErrorReporting", data);

        var bugcheck = new OSBugCheck(source);

        Assert.True(bugcheck.CanHandle(source));
        Assert.Equal("0x0000000a", bugcheck.BugCheckCode);
        Assert.Equal("0xffff800000000001", bugcheck.Parameter1);
        Assert.Equal("0x0000000000000002", bugcheck.Parameter2);
        Assert.Equal("0x0000000000000000", bugcheck.Parameter3);
        Assert.Equal("0xffff800000000004", bugcheck.Parameter4);
        Assert.Equal(@"C:\Windows\MEMORY.DMP", bugcheck.DumpFile);
        Assert.Equal("11111111-2222-3333-4444-555555555555", bugcheck.ReportId);
        Assert.Equal(source.TimeCreated, bugcheck.When);
    }

    private static EventObject CreateEvent(int id, string provider, string data) {
        var metadata = new NativeEventMetadata(
            provider, providerId: null, id, qualifiers: null, level: 2,
            task: null, opcode: null, keywords: null,
            timeCreated: new DateTime(2026, 11, 5, 10, 9, 31, DateTimeKind.Local),
            recordId: id, activityId: null, relatedActivityId: null,
            processId: null, threadId: null, logName: "System", machineName: "test-pc",
            userId: null, version: null);
        string xml = $"""
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <EventData>{data}</EventData>
            </Event>
            """;
        return new EventObject(metadata, xml, bookmark: null, queriedMachine: "test-pc", containerLog: "System");
    }
}