using EventViewerX.Reports.Live;
using Xunit;

namespace EventViewerX.Tests;

public class TestLiveEventQueryExecutor {
    [Fact]
    public void FormatTimeCreatedUtc_ShouldOmitMissingTimestamp() {
        Assert.Null(
            LiveEventQueryExecutor.FormatTimeCreatedUtc(
                DateTime.MinValue));
    }

    [Fact]
    public void FormatTimeCreatedUtc_ShouldPreservePresentTimestamp() {
        var timestamp = new DateTime(
            2026,
            7,
            25,
            21,
            52,
            0,
            DateTimeKind.Utc);

        Assert.Equal(
            "2026-07-25T21:52:00.0000000Z",
            LiveEventQueryExecutor.FormatTimeCreatedUtc(
                timestamp));
    }

    [Fact]
    public void Request_DefaultsToBoundedMaterialization() {
        Assert.Equal(1000, new LiveEventQueryRequest().MaxEvents);
    }

    [Fact]
    public void TryRead_ShouldFailForMissingLogName() {
        var ok = LiveEventQueryExecutor.TryRead(
            request: new LiveEventQueryRequest {
                LogName = string.Empty
            },
            result: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(LiveEventQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldFailForNegativeMaxEvents() {
        var ok = LiveEventQueryExecutor.TryRead(
            request: new LiveEventQueryRequest {
                LogName = "Application",
                MaxEvents = -1
            },
            result: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(LiveEventQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldClassifyNativeInvalidXPath() {
        if (!OperatingSystem.IsWindows()) return;

        bool ok = LiveEventQueryExecutor.TryRead(
            new LiveEventQueryRequest {
                LogName = "Application",
                XPath = "*[System[(EventID=]]",
                MaxEvents = 1
            },
            out _,
            out LiveEventQueryFailure? failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(
            LiveEventQueryFailureKind.InvalidQuery,
            failure!.Kind);
    }
}
