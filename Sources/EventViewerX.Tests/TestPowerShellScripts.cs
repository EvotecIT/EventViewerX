using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace EventViewerX.Tests {
    public class TestPowerShellScripts {
        [Fact]
        public void FragmentCacheReleasesACompleteScriptImmediately() {
            var cache = new PowerShellScriptFragmentCache(maxPendingScripts: 4, maxCachedEvents: 8);

            bool firstComplete = cache.TryAdd(
                "script-1",
                messageNumber: 2,
                messageTotal: 2,
                scriptText: "world",
                CreateEventObject(),
                out PowerShellScriptAssembly? first);
            bool secondComplete = cache.TryAdd(
                "script-1",
                messageNumber: 1,
                messageTotal: 2,
                scriptText: "hello ",
                CreateEventObject(),
                out PowerShellScriptAssembly? completed);

            Assert.False(firstComplete);
            Assert.Null(first);
            Assert.True(secondComplete);
            Assert.NotNull(completed);
            Assert.Equal("hello ", completed!.Parts[1]);
            Assert.Equal("world", completed.Parts[2]);
            Assert.Equal(2, completed.Events.Count);
            Assert.True(completed.IsComplete);
            Assert.Equal(0, cache.PendingScriptCount);
            Assert.Equal(0, cache.CachedEventCount);
        }

        [Fact]
        public void FragmentCacheEvictsTheOldestIncompleteScriptAtItsBounds() {
            var cache = new PowerShellScriptFragmentCache(maxPendingScripts: 2, maxCachedEvents: 2);

            cache.TryAdd("script-1", 1, 2, "one", CreateEventObject(), out _);
            cache.TryAdd("script-2", 1, 2, "two", CreateEventObject(), out _);
            cache.TryAdd("script-3", 1, 2, "three", CreateEventObject(), out _);

            Assert.True(cache.Contains("script-1"));
            Assert.True(cache.Contains("script-2"));
            Assert.False(cache.Contains("script-3"));
            Assert.Equal(2, cache.PendingScriptCount);
            Assert.Equal(2, cache.CachedEventCount);
            Assert.Equal(1, cache.EvictedScriptCount);
            Assert.Equal(1, cache.EvictedEventCount);
        }

        [Fact]
        public void BoundedScriptSelectionKeepsTheNewestEncounterInsteadOfTheFirstCompletion() {
            var selected = new List<KeyValuePair<long, RestoredPowerShellScript>>();
            var olderCompletion = new RestoredPowerShellScript { ScriptBlockId = "older" };
            var newerCompletion = new RestoredPowerShellScript { ScriptBlockId = "newer" };

            PowerShellEventEngine.AddBoundedRestoredPowerShellScript(selected, encounterOrder: 1, olderCompletion, maxScripts: 1);
            PowerShellEventEngine.AddBoundedRestoredPowerShellScript(selected, encounterOrder: 0, newerCompletion, maxScripts: 1);

            KeyValuePair<long, RestoredPowerShellScript> retained = Assert.Single(selected);
            Assert.Equal(0, retained.Key);
            Assert.Same(newerCompletion, retained.Value);
            Assert.False(PowerShellEventEngine.CanFinalizeBoundedPowerShellScriptSelection(
                selected,
                maxScripts: 1,
                newestPendingEncounterOrder: 0));
            Assert.True(PowerShellEventEngine.CanFinalizeBoundedPowerShellScriptSelection(
                selected,
                maxScripts: 1,
                newestPendingEncounterOrder: 2));
        }

        [Fact]
        public void FragmentCacheRejectsUntrustedOversizedPartDeclarations() {
            var cache = new PowerShellScriptFragmentCache(maxPendingScripts: 2, maxCachedEvents: 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => cache.TryAdd(
                "script-1",
                messageNumber: 1,
                messageTotal: int.MaxValue,
                scriptText: "content",
                CreateEventObject(),
                out _));
        }

        [Fact]
        public void ScriptReconstructionUsesAvailablePartKeysInsteadOfTheDeclaredRange() {
            var assembly = new PowerShellScriptAssembly(
                "script-1",
                metaRecord: null,
                events: new[] { CreateEventObject() },
                parts: new Dictionary<int, string> {
                    [1] = "first",
                    [PowerShellEventEngine.MaximumPowerShellScriptPartCount] = "last"
                },
                expectedParts: int.MaxValue,
                isComplete: false);
            MethodInfo method = typeof(PowerShellEventEngine).GetMethod(
                "TryBuildRestoredPowerShellScript",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            object?[] arguments = { assembly, false, new[] { "not-present" }, null };

            bool matched = Assert.IsType<bool>(method.Invoke(null, arguments));

            Assert.False(matched);
        }

        [Fact]
        public void QueryExecutionInfoMakesBoundedOrIncompleteResultsMachineReadable() {
            var info = new PowerShellScriptQueryExecutionInfo();
            info.Reset("AD1", null, maxResults: 10, maxEventsScanned: 100);
            info.EventsScanned = 100;
            info.ScanLimitReached = true;

            Assert.True(info.MayBeIncomplete);
            Assert.Equal("AD1", info.MachineName);
            Assert.Equal(100, info.EventsScanned);
        }

        [Fact]
        public void QueryExecutionInfoReportsRemoteFailureAsIncomplete() {
            var info = new PowerShellScriptQueryExecutionInfo();
            info.Reset("AD1", null, maxResults: 10, maxEventsScanned: 100);
            info.RecordFailure(EventLogRemoteQueryFailureKind.HostUnavailable, "RPC unavailable");

            Assert.False(info.Succeeded);
            Assert.True(info.MayBeIncomplete);
            Assert.Equal(EventLogRemoteQueryFailureKind.HostUnavailable, info.FailureKind);
            Assert.Equal("RPC unavailable", info.FailureMessage);
        }

        [Fact]
        public void OutputLimitRequiresOneAdditionalMatchingResult() {
            var info = new PowerShellScriptQueryExecutionInfo();
            info.Reset(
                machineName: null,
                eventLogPath: null,
                maxResults: 2,
                maxEventsScanned: 0);

            Assert.True(info.TryRecordResult());
            Assert.True(info.TryRecordResult());
            Assert.False(info.OutputLimitReached);
            Assert.Equal(2, info.ResultsReturned);

            Assert.False(info.TryRecordResult());
            Assert.True(info.OutputLimitReached);
            Assert.Equal(2, info.ResultsReturned);
        }

        [Fact]
        public void ExecutionLimitsRemainIndependentWhenDiagnosticsAreReused() {
            var shared =
                new PowerShellScriptQueryExecutionInfo();
            int firstReturned = 0;
            int secondReturned = 0;

            shared.Reset(
                machineName: null,
                eventLogPath: null,
                maxResults: 1,
                maxEventsScanned: 0);
            shared.Reset(
                machineName: null,
                eventLogPath: null,
                maxResults: 0,
                maxEventsScanned: 0);

            Assert.True(
                PowerShellEventEngine
                    .TryRecordPowerShellScriptExecutionResult(
                        maxEvents: 1,
                        ref firstReturned,
                        shared));
            Assert.False(
                PowerShellEventEngine
                    .TryRecordPowerShellScriptExecutionResult(
                        maxEvents: 1,
                        ref firstReturned,
                        shared));
            Assert.True(
                PowerShellEventEngine
                    .TryRecordPowerShellScriptExecutionResult(
                        maxEvents: 0,
                        ref secondReturned,
                        shared));
            Assert.Equal(1, firstReturned);
            Assert.Equal(1, secondReturned);
        }

        [Fact]
        public void ReconstructedOutputLimitUsesTheSameOneAdditionalMatchContract() {
            var info = new PowerShellScriptQueryExecutionInfo();
            info.Reset(
                machineName: null,
                eventLogPath: null,
                maxResults: 2,
                maxEventsScanned: 0);

            Assert.True(info.TryRecordMatchingResult());
            Assert.True(info.TryRecordMatchingResult());
            Assert.False(info.OutputLimitReached);

            Assert.False(info.TryRecordMatchingResult());
            Assert.True(info.OutputLimitReached);
            Assert.Equal(0, info.ResultsReturned);
        }

        [Fact]
        public void ScanLimitUsesOneCandidateLookahead() {
            var exact = new PowerShellScriptScanLimit(
                maximumEvents: 2);

            Assert.Equal(3, exact.NativeReadLimit);
            Assert.True(exact.TryAcceptCandidate());
            Assert.True(exact.TryAcceptCandidate());
            Assert.False(exact.LimitReached);

            var truncated = new PowerShellScriptScanLimit(
                maximumEvents: 2);

            Assert.True(truncated.TryAcceptCandidate());
            Assert.True(truncated.TryAcceptCandidate());
            Assert.False(
                truncated.TryAcceptCandidate());
            Assert.Equal(2, truncated.EventsScanned);
            Assert.True(truncated.LimitReached);
        }

        [Fact]
        public void UnlimitedScanDoesNotImposeANativeReadCap() {
            var limit = new PowerShellScriptScanLimit(
                maximumEvents: 0);

            Assert.Equal(0, limit.NativeReadLimit);
            Assert.True(limit.TryAcceptCandidate());
            Assert.False(limit.LimitReached);
        }

        [Fact]
        public void GetPowerShellScriptsRejectsInvalidBoundsBeforeOpeningTheLog() {
            Assert.Throws<ArgumentOutOfRangeException>(() => PowerShellEventEngine.GetPowerShellScripts(
                PowerShellEdition.WindowsPowerShell,
                maxScripts: -1).ToList());
            Assert.Throws<ArgumentOutOfRangeException>(() => PowerShellEventEngine.GetPowerShellScripts(
                PowerShellEdition.WindowsPowerShell,
                maxPendingScripts: 0).ToList());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PowerShellEventEngine
                    .GetPowerShellScriptExecution(
                        PowerShellEdition.WindowsPowerShell,
                        maxEvents: -1));
        }

        [Fact]
        public void PowerShellQueriesRejectUndefinedEditionsBeforeOpeningTheLog() {
            PowerShellEdition invalid =
                (PowerShellEdition)int.MaxValue;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PowerShellEventEngine.GetPowerShellScriptExecution(
                    invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PowerShellEventEngine.RestorePowerShellScripts(
                    invalid));
        }

        [Fact]
        public void ExecutionQueriesValidateAndResetAtCallTime() {
            var queryInfo =
                new PowerShellScriptQueryExecutionInfo();
            queryInfo.Reset(
                "stale",
                "stale.evtx",
                maxResults: 1,
                maxEventsScanned: 1);
            queryInfo.RecordFailure(
                EventLogRemoteQueryFailureKind.Timeout,
                "stale");

            IEnumerable<PowerShellScriptExecutionInfo> events =
                PowerShellEventEngine.GetPowerShellScriptExecution(
                    PowerShellEdition.WindowsPowerShell,
                    machineName: "server",
                    eventLogPath: "not-opened.evtx",
                    maxEvents: 2,
                    maxEventsScanned: 3,
                    executionInfo: queryInfo);

            Assert.NotNull(events);
            Assert.Equal("server", queryInfo.MachineName);
            Assert.Equal(
                "not-opened.evtx",
                queryInfo.EventLogPath);
            Assert.Equal(2, queryInfo.MaxResults);
            Assert.Equal(3, queryInfo.MaxEventsScanned);
            Assert.True(queryInfo.Succeeded);
        }

        [Fact]
        public void RestorePowerShellScriptsSnapshotsTextFiltersAtCallTime() {
            int enumerations = 0;

            IEnumerable<string> EnumerateFilters() {
                enumerations++;
                yield return "Get-Date";
            }

            IEnumerable<RestoredPowerShellScript> scripts =
                PowerShellEventEngine.RestorePowerShellScripts(
                    PowerShellEdition.WindowsPowerShell,
                    eventLogPath: "not-opened.evtx",
                    containsText: EnumerateFilters());

            Assert.Equal(1, enumerations);
            Assert.NotNull(scripts);
        }

        [Fact]
        public void GetPowerShellScriptsHonorsPreCancelledRequestsBeforeOpeningTheLog() {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => PowerShellEventEngine.GetPowerShellScripts(
                PowerShellEdition.WindowsPowerShell,
                null,
                null,
                null,
                null,
                false,
                null,
                0,
                0,
                PowerShellEventEngine.DefaultPowerShellScriptPendingLimit,
                PowerShellEventEngine.DefaultPowerShellScriptEventCacheLimit,
                cancellation.Token).ToList());
            Assert.Throws<OperationCanceledException>(() => PowerShellEventEngine.GetPowerShellScriptExecution(
                PowerShellEdition.WindowsPowerShell,
                null,
                null,
                null,
                null,
                0,
                0,
                cancellation.Token));
        }

        [Fact]
        public void RestoredScriptSaveContainsUntrustedMetadataInsideTheDestination() {
            string destination = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests." + Guid.NewGuid().ToString("N"));
            try {
                EventObject eventObject = CreateEventObject();
                SetSnapshotProperty(eventObject, nameof(EventObject.MachineName), "..\\..\\outside");
                SetSnapshotProperty(eventObject, nameof(EventObject.RecordId), (long?)1);
                SetSnapshotProperty(eventObject, nameof(EventObject.LogName), "PowerShell");
                SetSnapshotProperty(eventObject, nameof(EventObject.TimeCreated), DateTime.UtcNow);
                var script = new RestoredPowerShellScript {
                    ScriptBlockId = "..\\..\\payload",
                    Script = "Get-Date",
                    Events = new[] { eventObject }
                };

                string savedPath = script.Save(destination, unblock: true);
                string destinationPrefix = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                Assert.StartsWith(destinationPrefix, Path.GetFullPath(savedPath), StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(savedPath));
                Assert.DoesNotContain("..", Path.GetFileName(savedPath));
            } finally {
                if (Directory.Exists(destination)) {
                    Directory.Delete(destination, recursive: true);
                }
            }
        }

        [Fact]
        public void RestoredScriptSaveRefusesToOverwriteItsDeterministicDestination() {
            string destination = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests." + Guid.NewGuid().ToString("N"));
            try {
                EventObject eventObject = CreateEventObject();
                SetSnapshotProperty(eventObject, nameof(EventObject.MachineName), "server");
                SetSnapshotProperty(eventObject, nameof(EventObject.RecordId), (long?)1);
                SetSnapshotProperty(eventObject, nameof(EventObject.LogName), "PowerShell");
                SetSnapshotProperty(eventObject, nameof(EventObject.TimeCreated), DateTime.UtcNow);
                var script = new RestoredPowerShellScript {
                    ScriptBlockId = "block",
                    Script = "Get-Date",
                    Events = new[] { eventObject }
                };

                string savedPath = script.Save(destination, addComment: false);
                Assert.Throws<IOException>(() => script.Save(destination, addComment: false));
                Assert.Equal("Get-Date", File.ReadAllText(savedPath));
            } finally {
                if (Directory.Exists(destination)) {
                    Directory.Delete(destination, recursive: true);
                }
            }
        }

        [Fact]
        public void FailedRestoredScriptWriteRemovesItsPartialFile() {
            string destination = Path.Combine(
                Path.GetTempPath(),
                "EventViewerX.Tests." +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            string path = Path.Combine(
                destination,
                "partial.ps1");
            try {
                IOException exception =
                    Assert.Throws<IOException>(() =>
                        RestoredPowerShellScript.WriteNewFile(
                            path,
                            "Get-Date",
                            static (writer, _) => {
                                writer.Write("partial");
                                writer.Flush();
                                throw new IOException(
                                    "volume full");
                            }));

                Assert.Equal(
                    "volume full",
                    exception.Message);
                Assert.False(File.Exists(path));
            } finally {
                if (Directory.Exists(destination)) {
                    Directory.Delete(
                        destination,
                        recursive: true);
                }
            }
        }

        [Fact]
        public void RestoredScriptSaveRefusesAPreexistingHardLink() {
            string destination = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests." + Guid.NewGuid().ToString("N"));
            string outsidePath = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests.Outside." + Guid.NewGuid().ToString("N") + ".ps1");
            try {
                EventObject eventObject = CreateEventObject();
                SetSnapshotProperty(eventObject, nameof(EventObject.MachineName), "server");
                SetSnapshotProperty(eventObject, nameof(EventObject.RecordId), (long?)1);
                SetSnapshotProperty(eventObject, nameof(EventObject.LogName), "PowerShell");
                SetSnapshotProperty(eventObject, nameof(EventObject.TimeCreated), DateTime.UtcNow);
                var script = new RestoredPowerShellScript {
                    ScriptBlockId = "block",
                    Script = "Get-Date",
                    Events = new[] { eventObject }
                };

                string savedPath = script.Save(destination, addComment: false, unblock: true);
                File.Delete(savedPath);
                File.WriteAllText(outsidePath, "sentinel");
                if (!CreateHardLink(savedPath, outsidePath, IntPtr.Zero)) {
                    throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }

                Assert.Throws<IOException>(() => script.Save(destination, addComment: false, unblock: true));
                Assert.Equal("sentinel", File.ReadAllText(outsidePath));
            } finally {
                if (Directory.Exists(destination)) {
                    Directory.Delete(destination, recursive: true);
                }
                if (File.Exists(outsidePath)) {
                    File.Delete(outsidePath);
                }
            }
        }

        private static EventObject CreateEventObject() {
            return (EventObject)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EventObject));
        }

        [System.Runtime.InteropServices.DllImport("Kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

        private static void SetSnapshotProperty<T>(EventObject eventObject, string propertyName, T value) {
            typeof(EventObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(eventObject, value);
        }
    }
}
