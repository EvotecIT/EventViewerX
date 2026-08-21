using System;
using EventViewerX.Reports.Security;
using Xunit;

namespace EventViewerX.Tests;

public class TestSecurityEvtxQueryRequestNormalizer {
    [Fact]
    public void TryCreate_ShouldFailWhenFilePathMissing() {
        var ok = SecurityEvtxQueryRequestNormalizer.TryCreate(
            filePath: " ",
            startTimeUtc: null,
            endTimeUtc: null,
            maxEventsScanned: null,
            top: null,
            includeSamples: null,
            sampleSize: null,
            request: out _,
            error: out var error);

        Assert.False(ok);
        Assert.Equal("filePath is required.", error);
    }

    [Fact]
    public void TryCreate_ShouldFailWhenTimeRangeInvalid() {
        var ok = SecurityEvtxQueryRequestNormalizer.TryCreate(
            filePath: "security.evtx",
            startTimeUtc: new DateTime(2026, 2, 11, 10, 0, 0, DateTimeKind.Utc),
            endTimeUtc: new DateTime(2026, 2, 11, 9, 0, 0, DateTimeKind.Utc),
            maxEventsScanned: 10,
            top: 5,
            includeSamples: false,
            sampleSize: 5,
            request: out _,
            error: out var error);

        Assert.False(ok);
        Assert.Equal("startTimeUtc must be less than or equal to endTimeUtc.", error);
    }

    [Fact]
    public void TryCreate_ShouldApplyDefaultsWhenValuesMissing() {
        var ok = SecurityEvtxQueryRequestNormalizer.TryCreate(
            filePath: " security.evtx ",
            startTimeUtc: null,
            endTimeUtc: null,
            maxEventsScanned: null,
            top: null,
            includeSamples: null,
            sampleSize: null,
            request: out var request,
            error: out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("security.evtx", request.FilePath);
        Assert.Equal(SecurityEvtxQueryRequestNormalizer.DefaultMaxEventsScanned, request.MaxEventsScanned);
        Assert.Equal(SecurityEvtxQueryRequestNormalizer.DefaultTop, request.Top);
        Assert.Equal(SecurityEvtxQueryRequestNormalizer.DefaultSampleSize, request.SampleSize);
        Assert.False(request.IncludeSamples);
    }

    [Fact]
    public void TryCreate_ShouldClampOutOfRangeValues() {
        var ok = SecurityEvtxQueryRequestNormalizer.TryCreate(
            filePath: "security.evtx",
            startTimeUtc: null,
            endTimeUtc: null,
            maxEventsScanned: 1_000_000,
            top: 0,
            includeSamples: true,
            sampleSize: -42,
            request: out var request,
            error: out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(SecurityEvtxQueryRequestNormalizer.MaxEventsScannedCap, request.MaxEventsScanned);
        Assert.Equal(1, request.Top);
        Assert.True(request.IncludeSamples);
        Assert.Equal(1, request.SampleSize);
    }
}
