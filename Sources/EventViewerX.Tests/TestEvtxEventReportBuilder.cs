using EventViewerX.Reports.Evtx;
using Xunit;

namespace EventViewerX.Tests;

public class TestEvtxEventReportBuilder {
    [Fact]
    public void MissingTimestampIsProjectedAsNull() {
        Assert.Null(
            EvtxEventReportBuilder
                .FormatTimeCreatedUtc(
                    DateTime.MinValue));
    }

    [Fact]
    public void PresentTimestampIsProjectedAsUtc() {
        var timestamp = new DateTime(
            2026,
            7,
            25,
            10,
            30,
            0,
            DateTimeKind.Utc);

        Assert.Equal(
            "2026-07-25T10:30:00.0000000Z",
            EvtxEventReportBuilder
                .FormatTimeCreatedUtc(
                    timestamp));
    }

    [Fact]
    public void TryBuild_ShouldFailForNegativeMaxMessageChars() {
        var ok = EvtxEventReportBuilder.TryBuild(
            request: new EvtxQueryRequest { FilePath = "dummy.evtx" },
            includeMessage: true,
            maxMessageChars: -1,
            report: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryBuild_ShouldReturnNotFoundForMissingFile() {
        var ok = EvtxEventReportBuilder.TryBuild(
            request: new EvtxQueryRequest {
                FilePath = "C:/this/file/does/not/exist.evtx",
                MaxEvents = int.MaxValue
            },
            includeMessage: false,
            maxMessageChars: 1024,
            report: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.NotFound, failure!.Kind);
    }
}
