using System;
using EventViewerX.Reports.Live;
using Xunit;

namespace EventViewerX.Tests;

public class TestLiveStatsQueryExecutor {
    [Fact]
    public void TryBuild_ShouldFailForMissingLogName() {
        var ok = LiveStatsQueryExecutor.TryBuild(
            request: new LiveStatsQueryRequest {
                LogName = string.Empty
            },
            result: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(LiveStatsQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryBuild_ShouldFailForInvalidTimeRange() {
        var ok = LiveStatsQueryExecutor.TryBuild(
            request: new LiveStatsQueryRequest {
                LogName = "Security",
                StartTimeUtc = new DateTime(2026, 2, 10, 11, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc)
            },
            result: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(LiveStatsQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryBuild_ShouldClassifyNativeInvalidXPath() {
        if (!OperatingSystem.IsWindows()) return;

        bool ok = LiveStatsQueryExecutor.TryBuild(
            new LiveStatsQueryRequest {
                LogName = "Application",
                XPath = "*[System[(EventID=]]",
                MaxEventsScanned = 1
            },
            out _,
            out LiveStatsQueryFailure? failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(
            LiveStatsQueryFailureKind.InvalidQuery,
            failure!.Kind);
    }

    [Fact]
    public void BuildEffectiveXPath_IntersectsCustomSelectorWithNativeTimeRange() {
        var start = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        string xpath = LiveStatsQueryExecutor.BuildEffectiveXPath("*[System[EventID=16]]", start, end);

        Assert.Equal(
            "(*[System[EventID=16]])[System[TimeCreated[@SystemTime >= '2026-02-10T10:00:00.0000000Z' and @SystemTime <= '2026-02-10T11:00:00.0000000Z']]]",
            xpath);
    }

    [Fact]
    public void BuildEffectiveXPath_AppliesUtcRangeToWildcardSelector() {
        var start = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        string xpath = LiveStatsQueryExecutor.BuildEffectiveXPath("*", start, end);

        Assert.Contains("2026-02-10T10:00:00.0000000Z", xpath, StringComparison.Ordinal);
        Assert.Contains("2026-02-10T11:00:00.0000000Z", xpath, StringComparison.Ordinal);
    }

    [Fact]
    public void EventQueryAppliesTheRemoteTimeoutToSessionAndReads() {
        EventLogChannelQuery query =
            LiveEventChannelQueryFactory.Create(
                "Application",
                "server.example.test",
                "*",
                10,
                false,
                EventReadMode.Metadata,
                4321);

        Assert.Equal(
            4321,
            query.RemoteConnectionTimeoutMilliseconds);
        Assert.Equal(
            4321,
            query.RemoteReadTimeoutMilliseconds);
    }

    [Fact]
    public void MissingTimestampsAreNotProjectedIntoLiveBounds() {
        bool available =
            LiveStatsQueryExecutor
                .TryNormalizeCreatedTimeUtc(
                    DateTime.MinValue,
                    out DateTime createdUtc);

        Assert.False(available);
        Assert.Equal(
            default,
            createdUtc);
    }

    [Fact]
    public void TryBuild_ExecutesCustomSelectorWithManagedTimeRange() {
        if (!OperatingSystem.IsWindows()) return;
        if (!TestEnv.CanReadLog("System")) return;
        EventObject? latest = EventLogEngine.ReadChannel(
            "System",
            options: new EventLogQueryOptions {
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata
            }).SingleOrDefault();
        if (latest == null) return;

        DateTime createdUtc = latest.TimeCreated.ToUniversalTime();
        bool success = LiveStatsQueryExecutor.TryBuild(
            new LiveStatsQueryRequest {
                LogName = "System",
                XPath = $"*[System[EventID={latest.Id}]]",
                StartTimeUtc = createdUtc.AddSeconds(-1),
                EndTimeUtc = createdUtc.AddSeconds(1),
                MaxEventsScanned = 100
            },
            out LiveStatsQueryResult result,
            out LiveStatsQueryFailure? failure);

        Assert.True(success, failure?.Message);
        Assert.True(result.MatchedEvents >= 1);
    }
}
