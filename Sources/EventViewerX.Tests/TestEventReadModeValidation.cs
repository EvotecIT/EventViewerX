using EventViewerX.Reports.Evtx;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventReadModeValidation {
    private const EventReadMode UndefinedReadMode =
        (EventReadMode)99;

    [Fact]
    public void EngineEntryPointsRejectUndefinedReadModesImmediately() {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogEngine.ReadChannel(
                new EventLogChannelQuery("Application") {
                    ReadMode = UndefinedReadMode
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogEngine.ReadFile(
                new EventLogFileQuery("missing.evtx") {
                    ReadMode = UndefinedReadMode
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogEngine.ReadStructured(
                new EventLogStructuredQuery(
                    "<QueryList><Query Id=\"0\" Path=\"Application\"><Select>*</Select></Query></QueryList>") {
                    ReadMode = UndefinedReadMode
                }));
    }

    [Fact]
    public void QueryFactoriesRejectUndefinedReadModes() {
        var options = new EventLogQueryOptions {
            ReadMode = UndefinedReadMode
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogQueryFactory.ForChannels(
                new[] { "Application" },
                options: options));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogQueryFactory.ForFiles(
                new[] { "missing.evtx" },
                options: options));
    }

    [Fact]
    public void BatchAndSubscriptionEntryPointsRejectUndefinedReadModes() {
        var channel = new EventLogChannelQuery(
            "Application") {
            ReadMode = UndefinedReadMode
        };
        EventLogBatchQuery batch =
            EventLogBatchQuery.ForChannels(
                new[] { channel });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogBatchEngine.Read(batch));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventLogSubscription(
                new EventLogSubscriptionQuery(
                    "Application") {
                    ReadMode = UndefinedReadMode
                },
                _ => { }));
    }

    [Fact]
    public void AsyncBatchRejectsUndefinedReadModesBeforeContinueOnError() {
        int failures = 0;
        EventLogBatchQuery batch =
            EventLogBatchQuery.ForChannels(
                new[] {
                    new EventLogChannelQuery(
                        "Application") {
                        ReadMode = UndefinedReadMode
                    }
                });
        batch.ContinueOnError = true;
        batch.FailureHandler = _ =>
            Interlocked.Increment(ref failures);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogBatchEngine.ReadAsync(batch));
        Assert.Equal(0, failures);
    }

    [Fact]
    public void EvtxRequestRejectsUndefinedReadModesBeforeOpeningTheFile() {
        bool success = EvtxQueryExecutor.TryForEachEvent(
            new EvtxQueryRequest {
                FilePath = "missing.evtx",
                ReadMode = UndefinedReadMode
            },
            _ => true,
            out EvtxQueryFailure? failure);

        Assert.False(success);
        Assert.NotNull(failure);
        Assert.Equal(
            EvtxQueryFailureKind.InvalidArgument,
            failure!.Kind);
        Assert.Contains(
            "readMode",
            failure.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
