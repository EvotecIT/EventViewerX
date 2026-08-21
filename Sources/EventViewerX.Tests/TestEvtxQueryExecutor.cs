using System;
using System.IO;
using EventViewerX.Reports.Evtx;
using EventViewerX.Reports.Security;
using EventViewerX.Reports.Stats;
using Xunit;

namespace EventViewerX.Tests;

public class TestEvtxQueryExecutor {
    [Fact]
    public void TryRead_ShouldFailForMissingFilePath() {
        var request = new EvtxQueryRequest {
            FilePath = string.Empty
        };

        var ok = EvtxQueryExecutor.TryRead(request, out _, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryForEachEvent_ShouldFailForMissingFilePath() {
        var request = new EvtxQueryRequest {
            FilePath = string.Empty
        };

        var ok = EvtxQueryExecutor.TryForEachEvent(request, _ => true, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryForEachEvent_ShouldFailForMissingHandler() {
        var request = new EvtxQueryRequest {
            FilePath = "dummy.evtx"
        };

        var ok = EvtxQueryExecutor.TryForEachEvent(request, null!, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldFailForInvalidTimeRange() {
        var request = new EvtxQueryRequest {
            FilePath = "dummy.evtx",
            StartTimeUtc = new DateTime(2026, 2, 10, 11, 0, 0, DateTimeKind.Utc),
            EndTimeUtc = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc)
        };

        var ok = EvtxQueryExecutor.TryRead(request, out _, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldFailForInvalidEventIds() {
        var request = new EvtxQueryRequest {
            FilePath = "dummy.evtx",
            EventIds = new[] { 4624, -1 }
        };

        var ok = EvtxQueryExecutor.TryRead(request, out _, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldAcceptEventIdZero() {
        var request = new EvtxQueryRequest {
            FilePath = "C:/this/file/does/not/exist.evtx",
            EventIds = new[] { 0 }
        };

        var ok = EvtxQueryExecutor.TryRead(
            request,
            out _,
            out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(
            EvtxQueryFailureKind.NotFound,
            failure!.Kind);
    }

    [Fact]
    public void StatsQuery_ShouldAcceptEventIdZero() {
        var request =
            new EvtxStatsQueryRequest {
                FilePath =
                    "C:/this/file/does/not/exist.evtx",
                EventIds = new[] { 0 }
            };

        bool success =
            EvtxStatsQueryExecutor.TryBuild(
                request,
                out _,
                out EvtxQueryFailure? failure);

        Assert.False(success);
        Assert.NotNull(failure);
        Assert.Equal(
            EvtxQueryFailureKind.NotFound,
            failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldRejectEventIdsAboveWindowsRange() {
        var request = new EvtxQueryRequest {
            FilePath = "dummy.evtx",
            EventIds = new[] { 65536 }
        };

        var ok = EvtxQueryExecutor.TryRead(
            request,
            out _,
            out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(
            EvtxQueryFailureKind.InvalidArgument,
            failure!.Kind);
    }

    [Fact]
    public void TryRead_ShouldReturnNotFoundForMissingFile() {
        var request = new EvtxQueryRequest {
            FilePath = "C:/this/file/does/not/exist.evtx"
        };

        var ok = EvtxQueryExecutor.TryRead(request, out _, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.NotFound, failure!.Kind);
    }

    [Fact]
    public void TryForEachEvent_ShouldReturnNotFoundForMissingFile() {
        var request = new EvtxQueryRequest {
            FilePath = "C:/this/file/does/not/exist.evtx"
        };

        var ok = EvtxQueryExecutor.TryForEachEvent(request, _ => true, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.NotFound, failure!.Kind);
    }

    [Fact]
    public void CallbackMutationDoesNotChangeTheValidatedEventCap() {
        var request = new EvtxQueryRequest {
            FilePath = GetFixturePath(),
            MaxEvents = 2,
            ReadMode = EventReadMode.Metadata
        };
        int callbacks = 0;

        bool success =
            EvtxQueryExecutor.TryForEachEventWithInfo(
                request,
                _ => {
                    callbacks++;
                    request.MaxEvents = 0;
                    return true;
                },
                out EvtxQueryExecutionInfo executionInfo,
                out EvtxQueryFailure? failure);

        Assert.True(success);
        Assert.Null(failure);
        Assert.Equal(2, callbacks);
        Assert.Equal(2, executionInfo.EventsDelivered);
        Assert.True(executionInfo.Truncated);
    }

    [Fact]
    public void CallbackFileFailureIsNotClassifiedAsAQueryFailure() {
        var request = new EvtxQueryRequest {
            FilePath = GetFixturePath(),
            MaxEvents = 1,
            ReadMode = EventReadMode.Metadata
        };

        bool success =
            EvtxQueryExecutor.TryForEachEvent(
                request,
                static _ =>
                    throw new FileNotFoundException(
                        "Callback output was unavailable."),
                out EvtxQueryFailure? failure);

        Assert.False(success);
        Assert.NotNull(failure);
        Assert.Equal(
            EvtxQueryFailureKind.Exception,
            failure!.Kind);
        Assert.Equal(
            "Callback output was unavailable.",
            failure.Message);
    }

    [Fact]
    public void SecurityBuilder_TryBuildFromFile_ShouldSurfaceQueryFailure() {
        var request = new EvtxQueryRequest {
            FilePath = "C:/this/file/does/not/exist.evtx",
            EventIds = new[] { 4625 },
            ProviderName = "Microsoft-Windows-Security-Auditing"
        };

        var ok = SecurityFailedLogonsReportBuilder.TryBuildFromFile(
            request,
            includeSamples: false,
            sampleSize: 10,
            report: out _,
            failure: out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.NotFound, failure!.Kind);
    }

    [Fact]
    public void ReportBuilders_ShouldReturnInvalidArgumentForNullRequests() {
        AssertInvalid(
            EvtxEventReportBuilder.TryBuild(null!, false, 0, out _, out EvtxQueryFailure? eventFailure),
            eventFailure);
        AssertInvalid(
            EvtxStatsReportBuilder.TryBuildFromFile(null!, out _, out EvtxQueryFailure? statsFailure),
            statsFailure);
        AssertInvalid(
            SecurityUserLogonsReportBuilder.TryBuildFromFile(null!, false, 0, out _, out EvtxQueryFailure? userFailure),
            userFailure);
        AssertInvalid(
            SecurityFailedLogonsReportBuilder.TryBuildFromFile(null!, false, 0, out _, out EvtxQueryFailure? failedFailure),
            failedFailure);
        AssertInvalid(
            SecurityAccountLockoutsReportBuilder.TryBuildFromFile(null!, false, 0, out _, out EvtxQueryFailure? lockoutFailure),
            lockoutFailure);
    }

    private static void AssertInvalid(bool success, EvtxQueryFailure? failure) {
        Assert.False(success);
        Assert.NotNull(failure);
        Assert.Equal(EvtxQueryFailureKind.InvalidArgument, failure!.Kind);
    }

    private static string GetFixturePath() {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Tests",
                "Logs",
                "NamedFilterExamples.evtx"));
    }
}
