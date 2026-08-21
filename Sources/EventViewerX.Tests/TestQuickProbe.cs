using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;
using Xunit;

namespace EventViewerX.Tests {
    public class TestQuickProbe {
        private static readonly string DeterministicUnavailableHost =
            new string('x', 256);
        private const int DeterministicUnavailablePort = 1;

        [Fact]
        public void LocalSessionCreationReturnsBeforeTheStalledFactoryCompletes() {
            if (!OperatingSystem.IsWindows()) return;

            var stopwatch = Stopwatch.StartNew();
            using EventLogSessionOpenResult result = EventLogSessionManager.CreateSessionResult(
                null,
                "QuickProbe",
                "Application",
                timeoutMs: 100,
                localSessionFactory: () => {
                    Thread.Sleep(2000);
                    return new System.Diagnostics.Eventing.Reader.EventLogSession();
                });

            Assert.False(result.Success);
            Assert.Equal(EventLogSessionOpenStatus.Timeout, result.Status);
            Assert.Equal(100, result.TimeoutMs);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(1500), $"Elapsed {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        }

        [Fact]
        public void ManagedRemoteSessionRejectsExplicitAuthenticationWithoutCredential() {
            if (!OperatingSystem.IsWindows()) return;

            ArgumentException exception =
                Assert.Throws<ArgumentException>(() =>
                    EventLogSessionManager
                        .CreateSessionResult(
                            "eventviewerx-auth.invalid",
                            "QuickProbe",
                            "Application",
                            timeoutMs: 1000,
                            rpcProbeOverride:
                                static (_, _) => true,
                            remoteSessionFactory:
                                static _ => new System.Diagnostics
                                    .Eventing.Reader.EventLogSession(),
                            authentication:
                                EventLogAuthentication.Kerberos));

            Assert.Equal(
                "authentication",
                exception.ParamName);
            Assert.Contains(
                "requires a credential",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RemoteSessionCreationClassifiesAStalledOpenAsTimeout() {
            if (!OperatingSystem.IsWindows()) return;

            const string host = "eventviewerx-stalled-session.invalid";
            EventLogSessionManager.ClearHostCache(host);
            var stopwatch = Stopwatch.StartNew();
            try {
                using EventLogSessionOpenResult result = EventLogSessionManager.CreateSessionResult(
                    host,
                    "QuickProbe",
                    "Application",
                    timeoutMs: 100,
                    rpcProbeOverride: static (_, _) => true,
                    remoteSessionFactory: static _ => {
                        Thread.Sleep(2000);
                        return new System.Diagnostics.Eventing.Reader.EventLogSession();
                    });

                Assert.False(result.Success);
                Assert.Equal(EventLogSessionOpenStatus.Timeout, result.Status);
                Assert.Null(result.CachedUntilUtc);
                Assert.False(
                    EventLogSessionManager
                        .TryGetHostNegativeCacheExpiry(
                            host,
                            out _));
                using EventLogSessionOpenResult retry =
                    EventLogSessionManager.CreateSessionResult(
                        host,
                        "QuickProbe",
                        "Application",
                        timeoutMs: 1000,
                        rpcProbeOverride: static (_, _) => true,
                        remoteSessionFactory:
                            static _ => new System.Diagnostics
                                .Eventing.Reader.EventLogSession());
                Assert.True(retry.Success);
                Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(1500), $"Elapsed {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            } finally {
                EventLogSessionManager.ClearHostCache(host);
            }
        }

        [Fact]
        public void RpcProbeBudgetTimeoutDoesNotMarkTheHostUnreachable() {
            if (!OperatingSystem.IsWindows()) return;

            const string host =
                "eventviewerx-rpc-budget-timeout.invalid";
            EventLogSessionManager.ClearHostCache(host);
            try {
                using EventLogSessionOpenResult result =
                    EventLogSessionManager.CreateSessionResult(
                        host,
                        "QuickProbe",
                        "Application",
                        timeoutMs: 100,
                        rpcProbeStatusOverride:
                            static (_, _) =>
                                Native.RpcEndpointProbeStatus
                                    .TimedOut);

                Assert.False(result.Success);
                Assert.Equal(
                    EventLogSessionOpenStatus.Timeout,
                    result.Status);
                Assert.Null(result.CachedUntilUtc);
                Assert.False(
                    EventLogSessionManager
                        .TryGetHostNegativeCacheExpiry(
                            host,
                            out _));
                using EventLogSessionOpenResult retry =
                    EventLogSessionManager.CreateSessionResult(
                        host,
                        "QuickProbe",
                        "Application",
                        timeoutMs: 1000,
                        rpcProbeStatusOverride:
                            static (_, _) =>
                                Native.RpcEndpointProbeStatus
                                    .Connected,
                        remoteSessionFactory:
                            static _ => new System.Diagnostics
                                .Eventing.Reader.EventLogSession());
                Assert.True(retry.Success);
            } finally {
                EventLogSessionManager.ClearHostCache(host);
            }
        }

        [Fact]
        public void AdmissionTimeoutDoesNotMarkAHealthyRemoteHostUnreachable() {
            if (!OperatingSystem.IsWindows()) return;

            const string host =
                "eventviewerx-admission-timeout.invalid";
            EventLogSessionManager.ClearHostCache(host);
            try {
                using EventLogSessionOpenResult result =
                    EventLogSessionManager.CreateSessionResult(
                        host,
                        "QuickProbe",
                        "Application",
                        timeoutMs: 1000,
                        rpcProbeOverride:
                            static (_, _) => true,
                        remoteSessionFactory:
                            static _ => throw new Native
                                .BoundedNativeOperationAdmissionTimeoutException(
                                    "Native admission timed out."));

                Assert.False(result.Success);
                Assert.Equal(
                    EventLogSessionOpenStatus.Timeout,
                    result.Status);
                Assert.Null(result.CachedUntilUtc);
                Assert.False(
                    EventLogSessionManager
                        .TryGetHostNegativeCacheExpiry(
                            host,
                            out _));
            } finally {
                EventLogSessionManager.ClearHostCache(host);
            }
        }

        [Fact]
        public void SessionCreationHonorsCancellationDuringRpcProbe() {
            if (!OperatingSystem.IsWindows()) return;

            const string host =
                "eventviewerx-canceled-rpc-probe.invalid";
            EventLogSessionManager.ClearHostCache(host);
            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(100));
            using var release = new ManualResetEventSlim();
            var stopwatch = Stopwatch.StartNew();
            try {
                Assert.Throws<OperationCanceledException>(() =>
                    EventLogSessionManager.CreateSessionResult(
                        host,
                        "QuickProbe",
                        "Application",
                        timeoutMs: 5000,
                        rpcProbeOverride: (_, _) => {
                            release.Wait();
                            return true;
                        },
                        cancellationToken:
                            cancellation.Token));

                Assert.True(
                    stopwatch.Elapsed <
                    TimeSpan.FromSeconds(5),
                    $"Cancellation took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            } finally {
                release.Set();
                EventLogSessionManager.ClearHostCache(host);
            }
        }

        [Fact]
        public void SessionCreationHonorsCancellationDuringNativeOpen() {
            if (!OperatingSystem.IsWindows()) return;

            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(100));
            using var release = new ManualResetEventSlim();
            var stopwatch = Stopwatch.StartNew();
            try {
                Assert.Throws<OperationCanceledException>(() =>
                    EventLogSessionManager.CreateSessionResult(
                        null,
                        "QuickProbe",
                        "Application",
                        timeoutMs: 5000,
                        localSessionFactory: () => {
                            release.Wait();
                            return new System.Diagnostics.Eventing.Reader
                                .EventLogSession();
                        },
                        cancellationToken:
                            cancellation.Token));

                Assert.True(
                    stopwatch.Elapsed <
                    TimeSpan.FromSeconds(5),
                    $"Cancellation took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            } finally {
                release.Set();
            }
        }

        [Fact]
        public void BlankMachineNameReportsTheResolvedLocalMachine() {
            if (!OperatingSystem.IsWindows()) return;

            var result = EventLogProbe.ProbeLatestEvent(
                "Definitely-Missing-EVX-Log",
                machineName: " ",
                timeout: TimeSpan.FromSeconds(2),
                maxEventsToScan: 1);

            Assert.False(string.IsNullOrWhiteSpace(result.Machine));
            Assert.Equal(EventLogProbeStatus.LogNotFound, result.Status);
            Assert.False(result.NativeQueryVerified);
        }

        [Fact]
        public void ProbeLatestEventHonorsPreCanceledOperation() {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                EventLogProbe.ProbeLatestEvent(
                    "Application",
                    cancellationToken: cancellation.Token));
        }

        [Fact]
        public void OptionalProbeStageCannotReportSuccessAfterCancellation() {
            using var cancellation =
                new CancellationTokenSource();

            Assert.Throws<OperationCanceledException>(() =>
                EventLogProbe.RunCancelableProbeStage(
                    () => {
                        cancellation.Cancel();
                        return 1L;
                    },
                    TimeSpan.FromSeconds(5),
                    cancellation.Token));
        }

        [Fact]
        public void OptionalProbeStageHonorsItsAbsoluteDeadline() {
            var stopwatch = Stopwatch.StartNew();
            using var release = new ManualResetEventSlim();

            try {
                Assert.Throws<TimeoutException>(() =>
                    EventLogProbe.RunCancelableProbeStage(
                        () => {
                            release.Wait(
                                TimeSpan.FromSeconds(30));
                            return 1L;
                        },
                        TimeSpan.FromSeconds(5),
                        CancellationToken.None));

                Assert.True(
                    stopwatch.Elapsed <
                    TimeSpan.FromSeconds(10),
                    $"Elapsed {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            } finally {
                release.Set();
            }
        }

        [Fact]
        public void OptionalProbeStageHonorsCancellationWhileBlocked() {
            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(100));
            var stopwatch = Stopwatch.StartNew();

            Assert.Throws<OperationCanceledException>(() =>
                EventLogProbe.RunCancelableProbeStage(
                    () => {
                        Thread.Sleep(
                            TimeSpan.FromSeconds(30));
                        return 1L;
                    },
                    TimeSpan.FromSeconds(5),
                    cancellation.Token));

            Assert.True(
                stopwatch.Elapsed <
                TimeSpan.FromSeconds(5),
                $"Elapsed {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        }

        [Fact]
        public void OptionalRecordCountTimeoutDoesNotDiscardTheProbeResult() {
            using var release =
                new ManualResetEventSlim();
            try {
                long? recordCount =
                    EventLogProbe.TryRunOptionalRecordCountStage(
                        () => {
                            release.Wait(
                                TimeSpan.FromSeconds(30));
                            return 42;
                        },
                        TimeSpan.FromMilliseconds(100),
                        CancellationToken.None,
                        CancellationToken.None);

                Assert.Null(recordCount);
            } finally {
                release.Set();
            }
        }

        [Fact]
        public void OptionalRecordCountStillPropagatesCallerCancellation() {
            using var cancellation =
                new CancellationTokenSource();

            Assert.Throws<OperationCanceledException>(() =>
                EventLogProbe.TryRunOptionalRecordCountStage(
                    () => {
                        cancellation.Cancel();
                        return 42;
                    },
                    TimeSpan.FromSeconds(5),
                    cancellation.Token,
                    cancellation.Token));
        }

        [Fact]
        public void RecordCountSessionOpenHonorsCancellation() {
            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(100));
            using var release =
                new ManualResetEventSlim();
            var stopwatch = Stopwatch.StartNew();
            try {
                Assert.Throws<OperationCanceledException>(() =>
                    EventLogProbe.TryReadRecordCount(
                        "Application",
                        machineName: null,
                        credential: null,
                        authentication:
                            EventLogAuthentication.Default,
                        remaining:
                            TimeSpan.FromSeconds(30),
                        cancellationToken:
                            cancellation.Token,
                        localSessionFactory: () => {
                            release.Wait();
                            return new System.Diagnostics
                                .Eventing.Reader
                                .EventLogSession();
                        }));

                Assert.True(
                    stopwatch.Elapsed <
                    TimeSpan.FromSeconds(5),
                    $"Cancellation took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            } finally {
                release.Set();
            }
        }

        [Fact]
        public void RecordCountInformationReadHonorsCancellation() {
            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(100));
            using var release =
                new ManualResetEventSlim();
            var stopwatch = Stopwatch.StartNew();
            try {
                Assert.Throws<OperationCanceledException>(() =>
                    EventLogProbe.TryReadRecordCount(
                        "Application",
                        machineName: null,
                        credential: null,
                        authentication:
                            EventLogAuthentication.Default,
                        remaining:
                            TimeSpan.FromSeconds(30),
                        cancellationToken:
                            cancellation.Token,
                        informationFactory: _ => {
                            release.Wait();
                            return null!;
                        }));

                Assert.True(
                    stopwatch.Elapsed <
                    TimeSpan.FromSeconds(5),
                    $"Cancellation took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            } finally {
                release.Set();
            }
        }

        [Fact]
        public void ProbeLatestEventRejectsCredentialsForLocalSessions() {
            var credential = new NetworkCredential(
                "eventviewerx-test",
                "not-used");

            Assert.Throws<ArgumentException>(() =>
                EventLogProbe.ProbeLatestEvent(
                    "Application",
                    credential: credential));
        }

        [Fact]
        public void ProbeTimestampScanSkipsRecordsWithoutTimestamps() {
            DateTime expected =
                new DateTime(
                    2026,
                    7,
                    24,
                    20,
                    15,
                    0,
                    DateTimeKind.Utc);
            EventObject missing =
                CreateEventWithTimestamp(
                    DateTime.MinValue);
            EventObject usable =
                CreateEventWithTimestamp(
                    expected);

            DateTime? actual =
                EventLogProbe.FindFirstUsableTimestampUtc(
                    new[] {
                        missing,
                        usable
                    },
                    maxEventsToScan: 2,
                    out int scanned,
                    out bool limitReached);

            Assert.Equal(2, scanned);
            Assert.Equal(expected, actual);
            Assert.False(limitReached);
        }

        [Fact]
        public void ProbeTimestampScanDistinguishesAnExhaustedSource() {
            DateTime? actual =
                EventLogProbe.FindFirstUsableTimestampUtc(
                    new[] {
                        CreateEventWithTimestamp(
                            DateTime.MinValue),
                        CreateEventWithTimestamp(
                            DateTime.MinValue)
                    },
                    maxEventsToScan: 2,
                    out int scanned,
                    out bool limitReached);

            Assert.Equal(2, scanned);
            Assert.Null(actual);
            Assert.False(limitReached);
        }

        [Fact]
        public void ProbeTimestampScanReportsOnlyRealTruncation() {
            DateTime? actual =
                EventLogProbe.FindFirstUsableTimestampUtc(
                    new[] {
                        CreateEventWithTimestamp(
                            DateTime.MinValue),
                        CreateEventWithTimestamp(
                            DateTime.MinValue),
                        CreateEventWithTimestamp(
                            DateTime.MinValue)
                    },
                    maxEventsToScan: 2,
                    out int scanned,
                    out bool limitReached);

            Assert.Equal(2, scanned);
            Assert.Null(actual);
            Assert.True(limitReached);
        }

        [Fact]
        public void ProbeLatestEvent_DoesNotWriteBoundaryDiagnostics() {
            if (!OperatingSystem.IsWindows()) return;

            string host = DeterministicUnavailableHost;
            InternalLogger previous = Settings._logger;
            var logger = new InternalLogger();
            var warnings = new List<string>();
            var verboseMessages = new List<string>();
            logger.OnWarningMessage += (_, args) => warnings.Add(args.FullMessage);
            logger.OnVerboseMessage += (_, args) => verboseMessages.Add(args.FullMessage);
            Settings._logger = logger;
            EventLogSessionManager.ClearHostCache(host);
            try {
                EventLogProbeResult result = EventLogProbe.ProbeLatestEvent(
                    "Application",
                    machineName: host,
                    timeout: TimeSpan.FromSeconds(5),
                    maxEventsToScan: 1);

                Assert.Equal(EventLogProbeStatus.HostUnavailable, result.Status);
                Assert.Empty(warnings);
                Assert.Empty(verboseMessages);
            } finally {
                EventLogSessionManager.ClearHostCache(host);
                Settings._logger = previous;
            }
        }

        [Fact]
        public void NegativeCacheExpiresAndIsReevaluated() {
            if (!OperatingSystem.IsWindows()) return;

            string host = DeterministicUnavailableHost;
            var originalTtl = Settings.NegativeCacheTtlSeconds;
            var originalRpcTimeout = Settings.RpcProbeTimeoutMs;
            var originalSessionTimeout = Settings.SessionTimeoutMs;

            try {
                Settings.NegativeCacheTtlSeconds = 1;
                Settings.RpcProbeTimeoutMs = 200;
                Settings.SessionTimeoutMs = 600;
                EventLogSessionManager.ClearAllHostCache();

                var first = EventLogProbe.ProbeLatestEvent("Application", machineName: host, timeout: TimeSpan.FromMilliseconds(500), maxEventsToScan: 2);
                Assert.Equal(EventLogProbeStatus.HostUnavailable, first.Status);

                var cached = EventLogProbe.ProbeLatestEvent("Application", machineName: host, timeout: TimeSpan.FromMilliseconds(300), maxEventsToScan: 2);
                Assert.Equal(EventLogProbeStatus.HostUnavailable, cached.Status);
                Assert.Contains("cached as unreachable", cached.Message, StringComparison.OrdinalIgnoreCase);

                Thread.Sleep(1200);

                var afterTtl = EventLogProbe.ProbeLatestEvent("Application", machineName: host, timeout: TimeSpan.FromMilliseconds(500), maxEventsToScan: 2);
                Assert.Equal(EventLogProbeStatus.HostUnavailable, afterTtl.Status);
                Assert.DoesNotContain("cached as unreachable", afterTtl.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
            finally {
                Settings.NegativeCacheTtlSeconds = originalTtl;
                Settings.RpcProbeTimeoutMs = originalRpcTimeout;
                Settings.SessionTimeoutMs = originalSessionTimeout;
                EventLogSessionManager.ClearAllHostCache();
            }
        }

        [Fact]
        public void RpcProbeUsesConfigurablePort() {
            if (!OperatingSystem.IsWindows()) return;

            var originalPort = Settings.RpcProbePort;
            var originalRpcTimeout = Settings.RpcProbeTimeoutMs;

            try {
                Settings.RpcProbePort = DeterministicUnavailablePort;
                Settings.RpcProbeTimeoutMs = 200;
                EventLogSessionManager.ClearAllHostCache();

                var result = EventLogProbe.ProbeLatestEvent("Application", machineName: DeterministicUnavailableHost, timeout: TimeSpan.FromMilliseconds(500), maxEventsToScan: 2);

                Assert.Equal(EventLogProbeStatus.HostUnavailable, result.Status);
                Assert.Contains("RPC preflight", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("port 1", result.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally {
                Settings.RpcProbePort = originalPort;
                Settings.RpcProbeTimeoutMs = originalRpcTimeout;
                EventLogSessionManager.ClearAllHostCache();
            }
        }

        private static EventObject CreateEventWithTimestamp(
            DateTime timestamp) {

            var eventObject =
                (EventObject)RuntimeHelpers
                .GetUninitializedObject(
                    typeof(EventObject));
            typeof(EventObject)
                .GetField(
                    "<TimeCreated>k__BackingField",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)!
                .SetValue(
                    eventObject,
                    timestamp);
            return eventObject;
        }
    }
}
