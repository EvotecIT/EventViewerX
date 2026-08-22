using Xunit;
using EventViewerX.Native;
using System.Collections.Concurrent;

namespace EventViewerX.Tests;

[Collection("NativeOperationLifetime")]
public sealed class TestRemoteConnectionBudget {
    [Fact]
    public void RpcProbeHonorsCallerCancellation() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Native.RpcEndpointProbe.Probe(
                "192.0.2.1",
                135,
                30000,
                cancellation.Token));
    }

    [Fact]
    public void RpcProbeClassifiesDeadlineExpirationSeparately() {
        var incomplete =
            new TaskCompletionSource<object?>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        RpcEndpointProbeStatus status =
            RpcEndpointProbe.Probe(
                "eventviewerx-rpc-timeout.invalid",
                135,
                25,
                CancellationToken.None,
                connectAsyncOverride:
                    () => incomplete.Task,
                connectedOverride:
                    static () => false);

        Assert.Equal(
            RpcEndpointProbeStatus.TimedOut,
            status);
    }

    [Fact]
    public void RemoteReaderCachesOnlyDefinitiveRpcFailure() {
        const string host =
            "eventviewerx-native-rpc-timeout.invalid";
        EventLogSessionManager.ClearHostCache(host);
        try {
            Assert.Throws<TimeoutException>(() =>
                WindowsEventRemoteReader
                    .EnsureRpcEndpointAvailable(
                        host,
                        135,
                        25,
                        RpcEndpointProbeStatus.TimedOut));
            Assert.False(
                EventLogSessionManager
                    .TryGetHostNegativeCacheExpiry(
                        host,
                        out _));

            Assert.Throws<System.ComponentModel.Win32Exception>(
                () => WindowsEventRemoteReader
                    .EnsureRpcEndpointAvailable(
                        host,
                        135,
                        25,
                        RpcEndpointProbeStatus.Failed));
            Assert.True(
                EventLogSessionManager
                    .TryGetHostNegativeCacheExpiry(
                        host,
                        out _));
        } finally {
            EventLogSessionManager.ClearHostCache(host);
        }
    }

    [Fact]
    public void NativeOperationAdmissionHonorsCallerCancellation() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Native.BoundedNativeOperation.Acquire(
                30000,
                "timed out",
                cancellation.Token));
    }

    [Fact]
    public void CancelledNativeAdmissionDisposesItsOperationLease() {
        using var cancellation =
            new CancellationTokenSource();
        var owner =
            new TestDisposable();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            BoundedNativeOperation.Execute(
                static () => true,
                30000,
                "timed out",
                cancellation.Token,
                operationLease: owner));
        Assert.True(owner.IsDisposed);
    }

    [Fact]
    public void TimedOutNativeOperationRetainsItsOwnerUntilCompletion() {
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        var resource = new TestDisposable();
        var lifetime =
            new RetainedDisposable<TestDisposable>(
                resource);

        Assert.Throws<TimeoutException>(() =>
            BoundedNativeOperation.Execute(
                () => {
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(30));
                    return 1;
                },
                500,
                "The retained operation timed out.",
                operationLease:
                    lifetime.Retain()));
        Assert.True(started.IsSet);

        lifetime.Dispose();
        Assert.False(resource.IsDisposed);
        release.Set();
        Assert.True(
            SpinWait.SpinUntil(
                () => resource.IsDisposed,
                TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SubtractsCompletedSetupStages() {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        Thread.Sleep(25);

        int remaining =
            Native.WindowsEventRemoteReader
                .GetRemainingConnectionTimeout(
                    budget,
                    1000,
                    "timed out");

        Assert.InRange(remaining, 1, 999);
    }

    [Fact]
    public void FailsBeforeStartingTheNextStageWhenExhausted() {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        Thread.Sleep(20);

        Assert.Throws<TimeoutException>(() =>
            Native.WindowsEventRemoteReader
                .GetRemainingConnectionTimeout(
                    budget,
                    1,
                    "timed out"));
    }

    [Fact]
    public void BufferedRemoteProducersReleaseNativeSlotsWhileBackpressured() {
        const int producerCount =
            BoundedNativeOperation.MaximumConcurrentOperations;
        using var cancellation =
            new CancellationTokenSource();
        var buffers =
            Enumerable.Range(0, producerCount)
                .Select(_ =>
                    new BlockingCollection<EventObject>(1))
                .ToArray();
        Task[] producers =
            buffers.Select(buffer =>
                    Task.Factory.StartNew(
                        () => {
                            using IEnumerator<EventObject> events =
                                Enumerable.Range(0, 3)
                                    .Select(index =>
                                        CreateEvent(index))
                                    .GetEnumerator();
                            try {
                                WindowsEventRemoteReader.CopyToBuffer(
                                    events,
                                    buffer,
                                    maxEvents: 0,
                                    readTimeoutMilliseconds: 1000,
                                    slotTimeoutMessage:
                                        "Timed out waiting for a native read slot.",
                                    cancellation.Token);
                            } catch (OperationCanceledException)
                                when (cancellation.IsCancellationRequested) {
                            }
                        },
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default))
                .ToArray();
        try {
            Assert.True(
                SpinWait.SpinUntil(
                    () => buffers.All(
                        static buffer =>
                            buffer.Count == 1),
                    TimeSpan.FromSeconds(5)),
                "The remote producers did not reach buffer backpressure.");

            using IDisposable seventeenth =
                BoundedNativeOperation.Acquire(
                    1000,
                    "Backpressured readers retained all native slots.");
        } finally {
            cancellation.Cancel();
            Assert.True(
                Task.WaitAll(
                    producers,
                    TimeSpan.FromSeconds(5)),
                "The backpressured producers did not stop after cancellation.");
            foreach (BlockingCollection<EventObject> buffer in buffers) {
                buffer.Dispose();
            }
        }
    }

    private static EventObject CreateEvent(
        int id) {

        var metadata = new NativeEventMetadata(
            "EventViewerX.Tests",
            providerId: null,
            id,
            qualifiers: null,
            level: 4,
            task: null,
            opcode: null,
            keywords: null,
            timeCreated: DateTime.UtcNow,
            recordId: id,
            activityId: null,
            relatedActivityId: null,
            processId: null,
            threadId: null,
            logName: "Application",
            machineName: Environment.MachineName,
            userId: null,
            version: null);
        return new EventObject(
            metadata,
            Environment.MachineName,
            "Application");
    }

    private sealed class TestDisposable : IDisposable {
        internal bool IsDisposed { get; private set; }

        public void Dispose() {
            IsDisposed = true;
        }
    }
}
