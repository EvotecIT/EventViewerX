using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestForwardedEventsQuerySafety {
    [Fact]
    public void QueryFactoryKeepsCompleteForwardedFilterOutOfNativeXPath() {
        DateTime start = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
        DateTime end = start.AddHours(1);

        EventLogBatchQuery batch = EventLogQueryFactory.ForChannels(
            new[] { "ForwardedEvents", "System" },
            filter: new EventFilter {
                EventIds = new[] { 4624 },
                StartTime = start,
                EndTime = end
            },
            options: new EventLogQueryOptions {
                MaxEventsScanned = 250
            });

        EventLogChannelQuery forwarded = Assert.Single(batch.ChannelQueries);
        EventLogStructuredQuery system = Assert.Single(batch.StructuredQueries);
        Assert.Equal("ForwardedEvents", forwarded.LogName);
        Assert.Equal("*", forwarded.XPath);
        Assert.DoesNotContain("EventID", forwarded.XPath, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeCreated", forwarded.XPath, StringComparison.Ordinal);
        Assert.NotNull(forwarded.ManagedPredicate);
        Assert.Equal(250, forwarded.ManagedMaxEventsScanned);
        Assert.True(forwarded.ManagedPredicate!(Create(start.AddMinutes(30))));
        Assert.Equal(start, forwarded.ManagedStartTimeUtc);
        Assert.Equal(end, forwarded.ManagedEndTimeUtc);
        Assert.Contains("TimeCreated", system.QueryXml, StringComparison.Ordinal);
        Assert.Null(system.ManagedStartTimeUtc);
        Assert.Null(system.ManagedEndTimeUtc);
    }

    [Fact]
    public void ManagedWindowIsInclusiveAndStopsAfterOrderedBoundary() {
        DateTime start = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
        DateTime end = start.AddHours(1);
        EventObject atStart = Create(start);
        EventObject atEnd = Create(end);
        EventObject before = Create(start.AddTicks(-1));
        EventObject after = Create(end.AddTicks(1));

        Assert.True(ForwardedEventsQuerySafety.ShouldInclude(atStart, start, end));
        Assert.True(ForwardedEventsQuerySafety.ShouldInclude(atEnd, start, end));
        Assert.False(ForwardedEventsQuerySafety.ShouldInclude(before, start, end));
        Assert.False(ForwardedEventsQuerySafety.ShouldInclude(after, start, end));
        Assert.True(ForwardedEventsQuerySafety.HasCrossedWindow(
            before,
            oldest: false,
            start,
            end));
        Assert.True(ForwardedEventsQuerySafety.HasCrossedWindow(
            after,
            oldest: true,
            start,
            end));
        Assert.False(ForwardedEventsQuerySafety.HasCrossedWindow(
            atStart,
            oldest: false,
            start,
            end));
    }

    [Fact]
    public void LowLevelNativeFiltersFailBeforeOpeningForwardedEvents() {
        var channel = new EventLogChannelQuery("ForwardedEvents") {
            XPath = "*[System[TimeCreated[@SystemTime>='2026-08-18T10:00:00.0000000Z']]]"
        };
        var structured = EventLogStructuredQuery.ForChannels(
            new[] { "ForwardedEvents" },
            "*[System[EventID=4624]]");

        ArgumentException channelFailure = Assert.Throws<ArgumentException>(
            () => EventLogEngine.ReadChannel(channel).ToArray());
        ArgumentException structuredFailure = Assert.Throws<ArgumentException>(
            () => EventLogEngine.ReadStructured(structured).ToArray());

        Assert.Contains("Windows Server 2025", channelFailure.Message, StringComparison.Ordinal);
        Assert.Contains("Windows Server 2025", structuredFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsolidatorPreservesForwardedEventsAsSingleChannelQueries() {
        EventLogBatchQuery input = EventLogBatchQuery.ForChannels(
            new[] {
                new EventLogChannelQuery("ForwardedEvents") {
                    XPath = "*[System[EventID=4624]]"
                },
                new EventLogChannelQuery("System") {
                    XPath = "*[System[EventID=7040]]"
                }
            });

        EventLogBatchQuery consolidated = EventLogBatchConsolidator.Consolidate(input);

        Assert.Equal("ForwardedEvents", Assert.Single(consolidated.ChannelQueries).LogName);
        Assert.Contains("System", Assert.Single(consolidated.StructuredQueries).QueryXml);
    }

    [Fact]
    public void ManagedFilterPreservesNativeEventFilterSemantics() {
        var filter = new EventFilter {
            EventIds = new[] { 4624 },
            RecordIds = new long[] { 42 },
            MinimumRecordIdExclusive = 40,
            MaximumRecordIdExclusive = 50,
            ProviderNames = new[] { "TestProvider" },
            Levels = new byte[] { 0 },
            Keywords = new long[] { 0x10 },
            Data = new[] { "alice" },
            NamedData = new Dictionary<string, IReadOnlyList<string>> {
                ["TargetUserName"] = new[] { "alice" }
            },
            ExcludedNamedData = new Dictionary<string, IReadOnlyList<string>> {
                ["IpAddress"] = new[] { "192.0.2.99" }
            },
            ExcludedEventIds = new[] { 4625 }
        };
        Func<EventObject, bool> predicate =
            ManagedEventFilter.CreatePredicate(filter)!;
        EventObject matching = new EventObject(
            new SyntheticEventRecord(
                DateTime.UtcNow,
                "<Event><EventData><Data Name=\"TargetUserName\">alice</Data><Data Name=\"IpAddress\">192.0.2.10</Data></EventData></Event>"),
            "collector",
            EventReadMode.StructuredData);

        Assert.True(predicate(matching));

        filter.ExcludedNamedData =
            new Dictionary<string, IReadOnlyList<string>> {
                ["TargetUserName"] = new[] { "alice" }
            };
        Assert.False(ManagedEventFilter.CreatePredicate(filter)!(matching));
    }

    private static EventObject Create(DateTime timeCreated) {
        return new EventObject(
            new SyntheticEventRecord(timeCreated),
            "collector",
            EventReadMode.Metadata);
    }

    private sealed class SyntheticEventRecord : EventRecord {
        private readonly DateTime _timeCreated;
        private readonly string _xml;

        internal SyntheticEventRecord(
            DateTime timeCreated,
            string xml = "<Event />") {

            _timeCreated = timeCreated;
            _xml = xml;
        }

        public override string ProviderName => "TestProvider";
        public override string LogName => "Security";
        public override string MachineName => "source.ad.evotec.xyz";
        public override int Id => 4624;
        public override byte? Level => 0;
        public override int? Task => 0;
        public override long? Keywords => 0x10;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 1;
        public override int? ThreadId => 1;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => _timeCreated;
        public override int? Qualifiers => null;
        public override long? RecordId => 42;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        public override string FormatDescription() => string.Empty;
        public override string FormatDescription(IEnumerable<object> values) => string.Empty;
        public override string ToXml() => _xml;
        protected override void Dispose(bool disposing) { }
    }
}
