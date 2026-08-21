using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Serialization;
using System.Security.Principal;
using EventViewerX.Reports.Evtx;
using EventViewerX.Reports.Live;
using EventViewerX.Reports.Stats;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventObjectTimeCreated
{
    [Fact]
    public void TimeCreated_NullFallsBackToMinValue()
    {
        var record = new NullTimeEventRecord();
        var eo = new EventObject(record, "local", EventReadMode.Metadata);

        Assert.Equal(DateTime.MinValue, eo.TimeCreated);
    }

    [Fact]
    public void MissingTimeDoesNotBecomeAStatisticsExtremum()
    {
        var record = new NullTimeEventRecord();
        var eventObject =
            new EventObject(
                record,
                "local",
                EventReadMode.Metadata);
        var builder =
            new EvtxStatsReportBuilder();

        builder.Add(eventObject);

        Assert.Equal(1, builder.Scanned);
        Assert.Null(builder.MinUtc);
        Assert.Null(builder.MaxUtc);
    }

    [Fact]
    public void LiveProjectionPreservesMissingTimeAndRecordId() {
        var eventObject =
            new EventObject(
                new NullTimeEventRecord(),
                "local",
                EventReadMode.Metadata);

        LiveEventRow row =
            LiveEventQueryExecutor.ProjectRow(
                eventObject,
                includeMessage: false,
                maxMessageChars: 0);

        Assert.Null(row.TimeCreatedUtc);
        Assert.Null(row.RecordId);
    }

    [Fact]
    public void EvtxProjectionPreservesMissingTimeAndRecordId() {
        var eventObject =
            new EventObject(
                new NullTimeEventRecord(),
                "local",
                EventReadMode.Metadata);

        EvtxEventReportRow row =
            EvtxEventReportBuilder.ProjectRow(
                eventObject,
                includeMessage: false,
                maxMessageChars: 0);

        Assert.Null(row.TimeCreatedUtc);
        Assert.Null(row.RecordId);
    }

    [Fact]
    public void MissingMetadataRemainsNullAndUsesItsOwnStatisticsCount() {
        var eventObject =
            new EventObject(
                new NullTimeEventRecord(
                    level: null),
                "local",
                EventReadMode.Metadata);

        LiveEventRow live =
            LiveEventQueryExecutor.ProjectRow(
                eventObject,
                includeMessage: false,
                maxMessageChars: 0);
        EvtxEventReportRow offline =
            EvtxEventReportBuilder.ProjectRow(
                eventObject,
                includeMessage: false,
                maxMessageChars: 0);
        var builder =
            new EvtxStatsReportBuilder();
        builder.Add(eventObject);
        EvtxStatsReport statistics =
            builder.Build();

        Assert.Null(live.Level);
        Assert.Null(live.Task);
        Assert.Equal(0, live.Opcode);
        Assert.Null(live.Keywords);
        Assert.Null(offline.Level);
        Assert.Equal(1, statistics.Scanned);
        Assert.Equal(1, statistics.EventsWithoutLevel);
        Assert.Empty(statistics.ByLevel);
    }

    private sealed class NullTimeEventRecord : EventRecord
    {
        private readonly byte? _level;

        public NullTimeEventRecord(
            byte? level = 4) {

            _level = level;
        }

        public override string ProviderName => "TestProvider";
        public override string LogName => "TestLog";
        public override string MachineName => Environment.MachineName;
        public override int Id => 0;
        public override byte? Level => _level;
        public override int? Task => null;
        public override long? Keywords => null;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 0;
        public override int? ThreadId => 0;
        public override string LevelDisplayName => "Information";
        public override string FormatDescription() => string.Empty;
        public override string FormatDescription(IEnumerable<object> values) => string.Empty;
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => null;
        public override int? Qualifiers => null;
        public override long? RecordId => null;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        protected override void Dispose(bool disposing) { }
        public override string ToXml() => "<Event></Event>";
    }
}
