using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace EventViewerX.Tests;

[Collection("NativeOperationLifetime")]
public sealed class TestNativeEventEngineContracts {
    [Fact]
    public void CatalogEnumerationRetainsSessionUntilTimedOutWorkFinishes() {
        using var release =
            new ManualResetEventSlim();
        using var disposed =
            new ManualResetEventSlim();
        var lifetime =
            new Native.RetainedDisposable<
                CallbackDisposable>(
                new CallbackDisposable(
                    disposed.Set));
        try {
            Assert.Throws<TimeoutException>(() =>
                EventLogCatalog.EnumerateNamesBounded(
                    () => {
                        release.Wait();
                        return new[] {
                            "Application"
                        };
                    },
                    100,
                    "catalog enumeration timed out",
                    CancellationToken.None,
                    lifetime.Retain()));

            lifetime.Dispose();
            Assert.False(
                disposed.IsSet);
        } finally {
            release.Set();
            Assert.True(
                disposed.Wait(
                    TimeSpan.FromSeconds(5)));
            lifetime.Dispose();
        }
    }

    [Fact]
    public async Task CatalogEnumerationCancellationReturnsBeforeNativeWorkAndRetainsSession() {
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim();
        using var disposed =
            new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource();
        var lifetime =
            new Native.RetainedDisposable<
                CallbackDisposable>(
                new CallbackDisposable(
                    disposed.Set));
        Task<string[]> enumeration = Task.Run(() =>
            EventLogCatalog.EnumerateNamesBounded(
                () => {
                    started.TrySetResult(true);
                    release.Wait();
                    return new[] {
                        "Application"
                    };
                },
                5000,
                "catalog enumeration timed out",
                cancellation.Token,
                lifetime.Retain()));
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            enumeration,
            Task.Delay(
                TimeSpan.FromSeconds(5)));
        try {
            Assert.Same(
                enumeration,
                completed);
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                async () =>
                    await enumeration);
            lifetime.Dispose();
            Assert.False(
                disposed.IsSet);
        } finally {
            release.Set();
            Assert.True(
                disposed.Wait(
                    TimeSpan.FromSeconds(5)));
            lifetime.Dispose();
        }
    }

    [Fact]
    public async Task ProviderMetadataCancellationReturnsBeforeNativeWorkAndRetainsSession() {
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim();
        using var disposed =
            new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource();
        var lifetime =
            new Native.RetainedDisposable<
                CallbackDisposable>(
                new CallbackDisposable(
                    disposed.Set));
        Task<EventProviderMetadataSnapshot> snapshot =
            Task.Run(() =>
                EventLogCatalog.SnapshotProviderBounded(
                    () => {
                        started.TrySetResult(true);
                        release.Wait();
                        return null!;
                    },
                    "Stalled.Provider",
                    5000,
                    cancellation.Token,
                    lifetime.Retain()));
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            snapshot,
            Task.Delay(
                TimeSpan.FromSeconds(5)));
        try {
            Assert.Same(
                snapshot,
                completed);
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                async () =>
                    await snapshot);
            lifetime.Dispose();
            Assert.False(
                disposed.IsSet);
        } finally {
            release.Set();
            Assert.True(
                disposed.Wait(
                    TimeSpan.FromSeconds(5)));
            lifetime.Dispose();
        }
    }

    [Fact]
    public void ProviderCatalogValidatesBeforeReturningDeferredResults() {
        var query =
            new EventLogCatalogQuery {
                ConnectionTimeoutMilliseconds = 0
            };

        Assert.Throws<
            ArgumentOutOfRangeException>(() =>
                EventLogCatalog.GetProviders(
                 query));
    }

    [Fact]
    public void ProviderCatalogRejectsExplicitAuthenticationWithoutCredentialAtCallTime() {
        var query =
            new EventLogCatalogQuery {
                MachineName =
                    "eventviewerx-auth.invalid",
                Authentication =
                    EventLogAuthentication.Kerberos
            };

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EventLogCatalog.GetProviders(
                    query));

        Assert.Contains(
            "requires a credential",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalClearReturnsOnCancellationWhileNativeWorkRetainsOwnership() {
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource();
        Task clear = Task.Run(() =>
            EventLogMaintenance.ExecuteLocalClear(
                () => {
                    started.TrySetResult(true);
                    release.Wait();
                },
                cancellation.Token));
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            clear,
            Task.Delay(
                TimeSpan.FromSeconds(5)));
        try {
            Assert.Same(clear, completed);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () =>
                    await clear);
        } finally {
            release.Set();
        }
    }

    [Fact]
    public async Task RemoteClearReturnsOnCancellationWhileNativeWorkRetainsOwnership() {
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim();
        using var leaseReleased =
            new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource();
        var lease =
            new CallbackDisposable(
                leaseReleased.Set);
        Task clear = Task.Run(() =>
            EventLogMaintenance.ExecuteRemoteClear(
                () => {
                    started.TrySetResult(true);
                    release.Wait();
                },
                5000,
                cancellation.Token,
                lease));
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            clear,
            Task.Delay(
                TimeSpan.FromSeconds(5)));
        try {
            Assert.Same(clear, completed);
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                async () =>
                    await clear);
            Assert.False(
                leaseReleased.IsSet);
        } finally {
            release.Set();
        }
        Assert.True(
            leaseReleased.Wait(
                TimeSpan.FromSeconds(5)));
    }

    private sealed class CallbackDisposable :
        IDisposable {
        private readonly Action _dispose;

        internal CallbackDisposable(
            Action dispose) {

            _dispose = dispose;
        }

        public void Dispose() {
            _dispose();
        }
    }

    [Fact]
    public void RemoteSessionUsesTheRequiredReservedNativeTimeout() {
        Assert.Equal(
            0,
            Native.WindowsEventRemoteSession
                .EvtOpenSessionReservedTimeout);
    }

    [Fact]
    public void BoundedNativeOperationHonorsCancellationAndCleansLateResult() {
        using var release = new ManualResetEventSlim();
        using var cleaned = new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource(50);

        Assert.Throws<OperationCanceledException>(() =>
            Native.BoundedNativeOperation.Execute(
                () => {
                    release.Wait();
                    return new object();
                },
                5000,
                "operation timed out",
                cancellation.Token,
                _ => cleaned.Set()));

        release.Set();
        Assert.True(
            cleaned.Wait(5000),
            "The late native result was not cleaned after cancellation.");
    }

    [Fact]
    public void CompletedNativeResultIsCleanedWhenAcceptanceObservesCancellation() {
        using var completed = new ManualResetEventSlim();
        using var cleaned = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() =>
            Native.BoundedNativeOperation.Execute(
                () => {
                    completed.Set();
                    return new object();
                },
                5000,
                "operation timed out",
                cancellation.Token,
                _ => cleaned.Set(),
                operationAccepted: () => {
                    Assert.True(completed.Wait(5000));
                    cancellation.Cancel();
                }));

        Assert.True(
            cleaned.Wait(5000),
            "The completed native result was not cleaned after cancellation won acceptance.");
    }

    [Fact]
    public void BoundedNativeOperationRejectsResultsCompletedAfterItsAbsoluteDeadline() {
        using var cleaned = new ManualResetEventSlim();

        Assert.Throws<TimeoutException>(() =>
            Native.BoundedNativeOperation.Execute(
                () => {
                    Thread.Sleep(150);
                    return new object();
                },
                50,
                "operation timed out",
                CancellationToken.None,
                _ => cleaned.Set()));

        Assert.True(
            cleaned.Wait(5000),
            "The result completed after the absolute deadline was not cleaned.");
    }

    [Fact]
    public void BoundedNativeOperationClassifiesLateFaultsAsTimeouts() {
        TimeoutException exception = Assert.Throws<TimeoutException>(() =>
            Native.BoundedNativeOperation.Execute<object>(
                () => {
                    Thread.Sleep(75);
                    throw new InvalidOperationException("late native failure");
                },
                50,
                "operation timed out",
                CancellationToken.None,
                operationAccepted: () => Thread.Sleep(150)));

        Assert.Equal("operation timed out", exception.Message);
    }

    [Fact]
    public void BookmarkMaterializationIsExplicitForQueryProjections() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();

        EventObject withoutBookmark =
            EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        MaxEvents = 1,
                        ReadMode = EventReadMode.Message
                    })
                .Single();
        EventObject withBookmark =
            EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        MaxEvents = 1,
                        ReadMode = EventReadMode.Message,
                        IncludeBookmark = true
                    })
                .Single();
        EventObject metadataWithBookmark =
            EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        MaxEvents = 1,
                        ReadMode = EventReadMode.Metadata,
                        IncludeBookmark = true
                    })
                .Single();
        EventObject rawXmlWithoutBookmark =
            EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        MaxEvents = 1,
                        ReadMode = EventReadMode.RawXml
                    })
                .Single();
        EventObject rawXmlWithBookmark =
            EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        MaxEvents = 1,
                        ReadMode = EventReadMode.RawXml,
                        IncludeBookmark = true
                    })
                .Single();

        Assert.Null(withoutBookmark.Bookmark);
        Assert.NotNull(withBookmark.Bookmark);
        Assert.NotNull(metadataWithBookmark.Bookmark);
        Assert.False(string.IsNullOrWhiteSpace(metadataWithBookmark.BookmarkXml));
        Assert.Null(rawXmlWithoutBookmark.Bookmark);
        Assert.NotNull(rawXmlWithBookmark.Bookmark);
        Assert.False(string.IsNullOrWhiteSpace(
            rawXmlWithBookmark.XMLData));
    }

    [Fact]
    public void FileReadabilityProbePreservesAccessDenied() {
        UnauthorizedAccessException exception =
            Assert.Throws<UnauthorizedAccessException>(() =>
                EventLogEngine.EnsureFileReadable(
                    "protected.evtx",
                    static _ => throw new UnauthorizedAccessException(
                        "Access denied.")));

        Assert.Equal("Access denied.", exception.Message);
    }

    [Fact]
    public void ChannelCatalogCanExcludeAnalyticAndDebugChannels() {
        if (!OperatingSystem.IsWindows()) return;

        IReadOnlyList<string> regular =
            EventLogCatalog.GetChannelNames(
                channelPatterns: new[] { "*" },
                includeAnalyticDebug: false);

        foreach (string channel in regular) {
            using var configuration =
                new System.Diagnostics.Eventing.Reader.EventLogConfiguration(
                    channel);
            Assert.NotEqual(
                System.Diagnostics.Eventing.Reader.EventLogType.Analytical,
                configuration.LogType);
            Assert.NotEqual(
                System.Diagnostics.Eventing.Reader.EventLogType.Debug,
                configuration.LogType);
        }
    }

    [Fact]
    public void ChannelCatalogPreservesAnExplicitAnalyticChannel() {
        if (!OperatingSystem.IsWindows()) return;

        string? analytic = EventLogCatalog
            .GetChannelNames(
                channelPatterns: new[] { "*" },
                includeAnalyticDebug: true)
            .FirstOrDefault(channel => {
                try {
                    using var configuration =
                        new System.Diagnostics.Eventing.Reader
                            .EventLogConfiguration(channel);
                    return configuration.LogType is
                        System.Diagnostics.Eventing.Reader
                            .EventLogType.Analytical or
                        System.Diagnostics.Eventing.Reader
                            .EventLogType.Debug;
                } catch {
                    return false;
                }
            });

        Assert.False(string.IsNullOrWhiteSpace(analytic));
        IReadOnlyList<string> channels =
            EventLogCatalog.GetChannelNames(
                channelPatterns: new[] {
                    analytic!,
                    "EventViewerX-No-Such-Channel-*"
                },
                includeAnalyticDebug: false);

        Assert.Contains(
            analytic!,
            channels,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoteChannelDefaultsToBoundedConnectionAndUnboundedRead() {
        var query = new EventLogChannelQuery("System");

        Assert.Equal(EventReadMode.Message, query.ReadMode);
        Assert.Equal(5000, query.RemoteConnectionTimeoutMilliseconds);
        Assert.Equal(0, query.RemoteReadTimeoutMilliseconds);
        Assert.Equal(EventLogAuthentication.Default, query.Authentication);
        Assert.Null(query.Credential);
        Assert.Equal(1, query.BookmarkOffset);
        Assert.True(query.StrictBookmark);
    }

    [Fact]
    public void GeneralQueriesDefaultToMessageInsteadOfEagerFullProjection() {
        Assert.Equal(
            EventReadMode.Message,
            new EventLogFileQuery(GetFixturePath()).ReadMode);
        Assert.Equal(
            EventReadMode.Message,
            new EventLogStructuredQuery(
                "<QueryList><Query Id='0'><Select Path='System'>*</Select></Query></QueryList>")
                .ReadMode);
        Assert.Equal(
            EventReadMode.Message,
            new EventLogSubscriptionQuery("System").ReadMode);
        Assert.Equal(
            EventReadMode.Message,
            new EventLogQueryOptions().ReadMode);
    }

    [Fact]
    public void TypedQueriesDefaultToMessageAndStructuredDataWithoutAttachments() {
        var query = new EventTypeQuery(
            new[] { EventType.ADUserLogonFailed });

        Assert.Equal(
            EventReadMode.StructuredDataAndMessage,
            query.ReadMode);
    }

    [Fact]
    public void LocalChannelRejectsCredentialsInsteadOfSilentlyIgnoringThem() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogChannelQuery("System") {
            Credential = new NetworkCredential("event-reader", "secret"),
            MaxEvents = 1,
            ReadMode = EventReadMode.Metadata
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            EventLogEngine.ReadChannel(query).ToArray());

        Assert.Contains("remote", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileQueryIsSnapshottedBeforeEnumerationStarts() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        var query = new EventLogFileQuery(path) {
            Oldest = true,
            MaxEvents = 2,
            ReadMode = EventReadMode.Metadata
        };

        IEnumerable<EventObject> events = EventLogEngine.ReadFile(query);
        query.Oldest = false;
        query.MaxEvents = 1;
        query.ReadMode = EventReadMode.Full;
        query.XPath = "*[System[EventID=999999]]";

        EventObject[] actual = events.ToArray();
        Assert.Equal(2, actual.Length);
        Assert.All(actual, static item => Assert.Equal(EventReadMode.Metadata, item.ReadMode));
        Assert.True(actual[0].RecordId < actual[1].RecordId);
    }

    [Fact]
    public void LocalChannelQueryIsSnapshottedBeforeEnumerationStarts() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogChannelQuery("System") {
            MaxEvents = 2,
            ReadMode = EventReadMode.Metadata
        };

        IEnumerable<EventObject> events = EventLogEngine.ReadChannel(query);
        query.MaxEvents = 1;
        query.ReadMode = EventReadMode.Full;
        query.XPath = "*[System[EventID=999999]]";

        EventObject[] actual = events.ToArray();
        Assert.Equal(2, actual.Length);
        Assert.All(actual, static item => Assert.Equal(EventReadMode.Metadata, item.ReadMode));
    }

    [Fact]
    public void StructuredQueryStreamsSeveralChannelsThroughOneNativeResultSet() {
        if (!OperatingSystem.IsWindows()) return;
        string queryXml = EventFilterCompiler.BuildChannelQueryXml(
            new[] { "System", "Application" });
        var query = new EventLogStructuredQuery(queryXml) {
            MaxEvents = 10,
            ReadMode = EventReadMode.Metadata
        };

        EventObject[] actual = EventLogEngine.ReadStructured(query).ToArray();

        Assert.Equal(10, actual.Length);
        Assert.All(actual, static item => {
            Assert.Contains(
                item.LogName,
                new[] { "System", "Application" },
                StringComparer.OrdinalIgnoreCase);
            Assert.Equal(item.LogName, item.ContainerLog, ignoreCase: true);
        });
    }

    [Fact]
    public void TolerantStructuredQueryReportsEveryFailedPathAndStreamsValidPaths() {
        if (!OperatingSystem.IsWindows()) return;
        const string missingLog =
            "EventViewerX-Missing-Structured-Query-Channel";
        string queryXml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query>" +
            $"<Query Id=\"1\" Path=\"{missingLog}\">" +
            $"<Select Path=\"{missingLog}\">*</Select>" +
            "</Query>" +
            "</QueryList>";
        var failures = new List<EventLogQueryFailure>();
        var query = new EventLogStructuredQuery(queryXml) {
            TolerateQueryErrors = true,
            FailureHandler = failures.Add,
            MaxEvents = 3,
            ReadMode = EventReadMode.Metadata
        };

        EventObject[] actual = EventLogEngine.ReadStructured(query).ToArray();

        Assert.Equal(3, actual.Length);
        EventLogQueryFailure failure = Assert.Single(failures);
        Assert.Equal(missingLog, failure.Source);
        Assert.IsType<Win32Exception>(failure.Exception);
    }

    [Fact]
    public void TolerantStructuredQueryDecodesEveryFailedPathBeforeReleasingNativeQueryInfo() {
        if (!OperatingSystem.IsWindows()) return;
        string[] missingLogs = {
            "EventViewerX-Missing-Structured-Query-Channel-1",
            "EventViewerX-Missing-Structured-Query-Channel-2",
            "EventViewerX-Missing-Structured-Query-Channel-3"
        };
        string queries = string.Join(
            string.Empty,
            missingLogs.Select((log, index) =>
                $"<Query Id=\"{index + 1}\" Path=\"{log}\">" +
                $"<Select Path=\"{log}\">*</Select>" +
                "</Query>"));
        string queryXml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query>" +
            queries +
            "</QueryList>";
        var failures = new List<EventLogQueryFailure>();
        var query = new EventLogStructuredQuery(queryXml) {
            TolerateQueryErrors = true,
            FailureHandler = failures.Add,
            MaxEvents = 1,
            ReadMode = EventReadMode.Metadata
        };

        EventObject[] actual =
            EventLogEngine.ReadStructured(query).ToArray();

        Assert.Single(actual);
        Assert.Equal(
            missingLogs,
            failures
                .Select(static failure => failure.Source)
                .ToArray());
        Assert.All(
            failures,
            static failure =>
                Assert.IsType<Win32Exception>(
                    failure.Exception));
    }

    [Fact]
    public void TolerantStructuredQueryCannotSilentlyReturnPartialResults() {
        if (!OperatingSystem.IsWindows()) return;
        const string missingLog =
            "EventViewerX-Missing-Structured-Query-Channel";
        string queryXml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query>" +
            $"<Query Id=\"1\" Path=\"{missingLog}\">" +
            $"<Select Path=\"{missingLog}\">*</Select>" +
            "</Query>" +
            "</QueryList>";
        var query = new EventLogStructuredQuery(queryXml) {
            TolerateQueryErrors = true,
            MaxEvents = 3,
            ReadMode = EventReadMode.Metadata
        };

        EventLogStructuredQueryException exception =
            Assert.Throws<EventLogStructuredQueryException>(() =>
                EventLogEngine.ReadStructured(query).ToArray());

        EventLogQueryFailure failure = Assert.Single(exception.Failures);
        Assert.Equal(missingLog, failure.Source);
    }

    [Fact]
    public void LocalFqdnUsesTheLocalChannelPath() {
        if (!OperatingSystem.IsWindows()) return;
        string fqdn;
        try {
            fqdn = Dns.GetHostEntry("").HostName;
        } catch {
            return;
        }
        if (string.Equals(fqdn, Environment.MachineName, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var query = new EventLogChannelQuery("System") {
            MachineName = fqdn,
            MaxEvents = 1,
            ReadMode = EventReadMode.Metadata
        };

        EventObject actual = Assert.Single(EventLogEngine.ReadChannel(query));

        Assert.Equal(Environment.MachineName, actual.GatheredFrom);
    }

    [Fact]
    public void CancellationStopsAFileEnumerationBetweenRecords() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogFileQuery(GetFixturePath()) {
            Oldest = true,
            ReadMode = EventReadMode.Metadata
        };
        using var cancellation = new CancellationTokenSource();
        int count = 0;

        Assert.Throws<OperationCanceledException>(() => {
            foreach (EventObject _ in EventLogEngine.ReadFile(query, cancellation.Token)) {
                count++;
                if (count == 3) {
                    cancellation.Cancel();
                }
            }
        });
        Assert.Equal(3, count);
    }

    [Fact]
    public void FileQueryCanResumeAfterANativeBookmark() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        EventObject first = Assert.Single(EventLogEngine.ReadFile(
            new EventLogFileQuery(path) {
                Oldest = true,
                MaxEvents = 1,
                ReadMode = EventReadMode.StructuredData,
                IncludeBookmark = true
            }));
        Assert.NotNull(first.Bookmark);

        EventObject resumed = Assert.Single(EventLogEngine.ReadFile(
            new EventLogFileQuery(path) {
                Oldest = true,
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata,
                BookmarkXml = first.Bookmark!.BookmarkXml
            }));

        Assert.True(resumed.RecordId > first.RecordId);
    }

    [Fact]
    public void FailedBookmarkSetupReleasesTheOfflineQueryHandle() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "bookmark-failure.evtx");
        File.Copy(GetFixturePath(), path);
        try {
            Assert.ThrowsAny<Exception>(() =>
                EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        Oldest = true,
                        ReadMode = EventReadMode.Metadata,
                        BookmarkXml = "<not-a-bookmark />"
                    }).ToList());

            File.Delete(path);
            Assert.False(File.Exists(path));
        } finally {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void NamedDataExclusionRetainsEventsWithoutTheNamedField() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        long unfiltered = EventLogEngine.ReadFile(
            new EventLogFileQuery(path) {
                Oldest = true,
                ReadMode = EventReadMode.Metadata
            }).LongCount();
        string queryXml = WindowsEventFilterBuilder.BuildWinEventFilter(
            namedDataExcludeFilter: [
                new System.Collections.Hashtable {
                    { "FieldThatDoesNotExistInFixture", "ExcludedValue" }
                }
            ],
            path: path);
        long filtered = EventLogEngine.ReadStructured(
            new EventLogStructuredQuery(queryXml) {
                SourceKind = EventLogQuerySourceKind.File,
                Oldest = true,
                ReadMode = EventReadMode.Metadata
            }).LongCount();

        Assert.True(unfiltered > 0);
        Assert.Equal(unfiltered, filtered);
    }

    [Fact]
    public void OfflineArchiveMetadataMatchesTheNativeRecordStream() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();

        EventLogFileInformation information =
            EventLogArchive.GetInformation(path);
        long streamed = EventLogEngine.ReadFile(
            new EventLogFileQuery(path) {
                Oldest = true,
                ReadMode = EventReadMode.Metadata
            }).LongCount();

        Assert.Equal(Path.GetFullPath(path), information.Path);
        Assert.Equal(streamed, information.RecordCount);
        Assert.True(information.FileSize > 0);
        Assert.True(information.OldestRecordNumber > 0);
    }

    [Fact]
    public void OfflineArchiveMetadataPreservesReadableValidationFailures() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();

        UnauthorizedAccessException exception =
            Assert.Throws<UnauthorizedAccessException>(
                () =>
                    Native.WindowsEventArchive
                        .GetFileInformation(
                            path,
                            static _ =>
                                throw new UnauthorizedAccessException(
                                    "Access denied.")));

        Assert.Equal(
            "Access denied.",
            exception.Message);
    }

    [Fact]
    public void ExportedArchiveCanReceiveProviderResourcesSeparately() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            string outputPath = Path.Combine(
                directory,
                "archived.evtx");
            EventLogExporter.ExportFile(
                new EventLogFileQuery(
                    GetFixturePath()),
                outputPath,
                EventExportFormat.Evtx,
                archiveResources: false);

            EventLogArchive.ArchiveResources(
                outputPath,
                System.Globalization.CultureInfo
                    .GetCultureInfo("en-US"));

            EventLogFileInformation information =
                EventLogArchive.GetInformation(
                    outputPath);
            Assert.True(information.RecordCount > 0);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AsyncFileStreamIsBoundedLazyAndOrdered() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogFileQuery(GetFixturePath()) {
            Oldest = true,
            MaxEvents = 5,
            ReadMode = EventReadMode.Metadata
        };
        var records = new List<long>();

        await foreach (EventObject eventObject in
                       EventLogEngine.ReadFileAsync(
                           query,
                           bufferCapacity: 2)) {
            records.Add(eventObject.RecordId!.Value);
        }

        Assert.Equal(5, records.Count);
        Assert.Equal(
            records.OrderBy(static value => value),
            records);
    }

    [Fact]
    public async Task AsyncFileStreamFreezesTheQueryBeforeEnumeration() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogFileQuery(GetFixturePath()) {
            Oldest = true,
            MaxEvents = 1,
            ReadMode = EventReadMode.Metadata
        };

        IAsyncEnumerable<EventObject> stream =
            EventLogEngine.ReadFileAsync(query);
        query.MaxEvents = 2;
        query.ReadMode = (EventReadMode)int.MaxValue;
        var records = new List<EventObject>();
        await foreach (EventObject eventObject in stream) {
            records.Add(eventObject);
        }

        Assert.Single(records);
    }

    [Fact]
    public async Task AsyncStructuredStreamFreezesTheQueryBeforeEnumeration() {
        if (!OperatingSystem.IsWindows()) return;
        EventLogStructuredQuery query =
            EventLogStructuredQuery.ForFiles(
                new[] { GetFixturePath() });
        query.Oldest = true;
        query.MaxEvents = 1;
        query.ReadMode = EventReadMode.Metadata;

        IAsyncEnumerable<EventObject> stream =
            EventLogEngine.ReadStructuredAsync(query);
        query.MaxEvents = 2;
        query.ReadMode = (EventReadMode)int.MaxValue;
        var records = new List<EventObject>();
        await foreach (EventObject eventObject in stream) {
            records.Add(eventObject);
        }

        Assert.Single(records);
    }

    [Fact]
    public async Task AsyncChannelStreamFreezesTheQueryBeforeEnumeration() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogChannelQuery("System") {
            Oldest = false,
            MaxEvents = 1,
            ReadMode = EventReadMode.Metadata
        };

        IAsyncEnumerable<EventObject> stream =
            EventLogEngine.ReadChannelAsync(query);
        query.MaxEvents = 2;
        query.ReadMode = (EventReadMode)int.MaxValue;
        var records = new List<EventObject>();
        await foreach (EventObject eventObject in stream) {
            records.Add(eventObject);
        }

        Assert.Single(records);
    }

    [Fact]
    public void QuerySnapshotsCloneMutableCredentials() {
        var credential =
            new System.Net.NetworkCredential(
                "original-user",
                "original-password",
                "original-domain");
        var channel = new EventLogChannelQuery("System") {
            MachineName = "remote.example",
            Credential = credential
        };
        EventLogStructuredQuery structured =
            EventLogStructuredQuery.ForChannels(
                new[] { "System" });
        structured.MachineName = "remote.example";
        structured.Credential = credential;

        EventLogChannelQuery channelSnapshot =
            EventLogQuerySnapshot.Copy(channel);
        EventLogStructuredQuery structuredSnapshot =
            EventLogQuerySnapshot.Copy(structured);
        credential.UserName = "changed-user";
        credential.Password = "changed-password";
        credential.Domain = "changed-domain";

        Assert.NotSame(
            credential,
            channelSnapshot.Credential);
        Assert.Equal(
            "original-user",
            channelSnapshot.Credential!.UserName);
        Assert.Equal(
            "original-password",
            structuredSnapshot.Credential!.Password);
        Assert.Equal(
            "original-domain",
            structuredSnapshot.Credential.Domain);
    }

    [Fact]
    public async Task AsyncFileStreamStopsBeforeDrainingBufferedEventsAfterCancellation() {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogFileQuery(GetFixturePath()) {
            Oldest = true,
            ReadMode = EventReadMode.Metadata
        };
        using var cancellation =
            new CancellationTokenSource();
        await using IAsyncEnumerator<EventObject> events =
            EventLogEngine.ReadFileAsync(
                    query,
                    bufferCapacity: 64,
                    cancellationToken: cancellation.Token)
                .GetAsyncEnumerator();

        Assert.True(
            await events.MoveNextAsync());
        await Task.Delay(100);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await events.MoveNextAsync()
                    .AsTask());
    }

    [Fact]
    public async Task AsyncStreamCancellationDoesNotWaitForAStalledProducer() {
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var exited = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation =
            new CancellationTokenSource();
        await using IAsyncEnumerator<EventObject> events =
            EventLogEngine.ReadAsync(
                    Source,
                    bufferCapacity: 1,
                    cancellation.Token)
                .GetAsyncEnumerator();
        Task<bool> moveNext =
            events.MoveNextAsync().AsTask();
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            moveNext,
            Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(moveNext, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await moveNext);
        release.Set();
        await exited.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        IEnumerable<EventObject> Source(
            CancellationToken cancellationToken) {

            entered.TrySetResult(true);
            try {
                release.Wait();
                cancellationToken.ThrowIfCancellationRequested();
                yield break;
            } finally {
                exited.TrySetResult(true);
            }
        }
    }

    [Fact]
    public void ProviderNameCatalogDoesNotRequireMetadataProjection() {
        if (!OperatingSystem.IsWindows()) return;

        IReadOnlyList<string> providers =
            EventLogCatalog.GetProviderNames(
                providerPatterns:
                    new[] { "Microsoft-Windows-Kernel-*" });

        Assert.NotEmpty(providers);
        Assert.All(providers, static provider =>
            Assert.StartsWith(
                "Microsoft-Windows-Kernel-",
                provider,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CorruptEventLogFailsLoudlyInsteadOfReturningAnEmptySuccess() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            string corruptPath = Path.Combine(directory, "corrupt.evtx");
            File.Copy(GetFixturePath(), corruptPath);
            using (FileStream stream = File.Open(corruptPath, FileMode.Open, FileAccess.Write, FileShare.None)) {
                stream.SetLength(4096);
            }
            var query = new EventLogFileQuery(corruptPath) {
                Oldest = true,
                ReadMode = EventReadMode.Metadata
            };

            Exception exception = Assert.ThrowsAny<Exception>(() =>
                EventLogEngine.ReadFile(query).ToArray());

            Assert.True(
                exception is Win32Exception ||
                exception is IOException ||
                exception is InvalidOperationException,
                $"Unexpected corruption failure: {exception.GetType().FullName}");
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CorruptEventLogDoesNotReplaceAnExistingExport() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            string corruptPath = Path.Combine(directory, "corrupt.evtx");
            File.Copy(GetFixturePath(), corruptPath);
            using (FileStream stream = File.Open(corruptPath, FileMode.Open, FileAccess.Write, FileShare.None)) {
                stream.SetLength(4096);
            }
            string outputPath = Path.Combine(directory, "events.jsonl");
            File.WriteAllText(outputPath, "preserve-me");
            var query = new EventLogFileQuery(corruptPath) {
                Oldest = true,
                ReadMode = EventReadMode.Full
            };

            Assert.ThrowsAny<Exception>(() =>
                EventLogExporter.ExportFile(
                    query,
                    outputPath,
                    EventExportFormat.JsonLines,
                    overwrite: true));

            Assert.Equal("preserve-me", File.ReadAllText(outputPath));
            Assert.Empty(Directory.GetFiles(directory, ".events.jsonl.*.tmp"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void XmlExportUsesTheSameRawContractForEveryReadMode() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            var metadataQuery = new EventLogFileQuery(GetFixturePath()) {
                Oldest = true,
                MaxEvents = 8,
                ReadMode = EventReadMode.Metadata
            };
            var fullQuery = new EventLogFileQuery(GetFixturePath()) {
                Oldest = true,
                MaxEvents = 8,
                ReadMode = EventReadMode.Full
            };

            EventExportResult metadata = EventLogExporter.ExportFile(
                metadataQuery,
                Path.Combine(directory, "metadata.xml"),
                EventExportFormat.Xml);
            EventExportResult full = EventLogExporter.ExportFile(
                fullQuery,
                Path.Combine(directory, "full.xml"),
                EventExportFormat.Xml);

            Assert.Equal(8, metadata.EventCount);
            Assert.Equal(metadata.Bytes, full.Bytes);
            Assert.Equal(metadata.Sha256, full.Sha256);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void StructuredOfflineQueryUsesArchivedMessageResources() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        var fileQuery =
            new EventLogFileQuery(path) {
                Oldest = true,
                MaxEvents = 1,
                ReadMode = EventReadMode.Full
            };
        EventLogStructuredQuery structuredQuery =
            EventLogStructuredQuery.ForFiles(
                new[] { path });
        structuredQuery.Oldest = true;
        structuredQuery.MaxEvents = 1;
        structuredQuery.ReadMode =
            EventReadMode.Full;

        EventObject file =
            Assert.Single(
                EventLogEngine.ReadFile(
                    fileQuery));
        EventObject structured =
            Assert.Single(
                EventLogEngine.ReadStructured(
                    structuredQuery));

        Assert.Equal(
            file.Message,
            structured.Message);
        Assert.False(
            string.IsNullOrWhiteSpace(
                structured.Message));
        Assert.Equal(
            file.MessageRenderStatus,
            structured.MessageRenderStatus);
        Assert.Equal(
            EventMessageRenderStatus.Rendered,
            structured.MessageRenderStatus);
    }

    [Fact]
    public void LocalChannelExportsJsonLinesWithoutAConsumerPipeline() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            string outputPath = Path.Combine(directory, "system.jsonl");
            var query = new EventLogChannelQuery("System") {
                MaxEvents = 3,
                ReadMode = EventReadMode.Message,
                MessageCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US")
            };

            EventExportResult result = EventLogExporter.ExportChannel(
                query,
                outputPath,
                EventExportFormat.JsonLines);

            Assert.Equal(3, result.EventCount);
            string[] lines = File.ReadAllLines(outputPath);
            Assert.Equal(3, lines.Length);
            Assert.All(lines, static line => {
                using JsonDocument document = JsonDocument.Parse(line);
                Assert.True(document.RootElement.GetProperty("recordId").GetInt64() > 0);
                Assert.Equal(
                    "en-US",
                    document.RootElement.GetProperty("messageCulture").GetString());
            });
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LocalChannelXmlExportDoesNotMutateTheRequestedReadMode() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            string outputPath = Path.Combine(directory, "system.xml");
            var query = new EventLogChannelQuery("System") {
                MaxEvents = 3,
                ReadMode = EventReadMode.Metadata
            };

            EventExportResult result = EventLogExporter.ExportChannel(
                query,
                outputPath,
                EventExportFormat.Xml);

            XDocument document = XDocument.Load(outputPath);
            XNamespace eventNamespace =
                "http://schemas.microsoft.com/win/2004/08/events/event";
            Assert.Equal(3, result.EventCount);
            Assert.Equal(3, document.Root!.Elements(eventNamespace + "Event").Count());
            Assert.Equal(EventReadMode.Metadata, query.ReadMode);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OfflineQuerySourceKindDoesNotDependOnTheFileExtension() {
        if (!OperatingSystem.IsWindows()) return;
        string directory = CreateTemporaryDirectory();
        try {
            string renamed = Path.Combine(directory, "renamed-event-archive");
            File.Copy(GetFixturePath(), renamed);

            EventObject first = EventLogEngine.ReadFile(new EventLogFileQuery(renamed) {
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata
            }).Single();

            Assert.Equal(EventLogQuerySourceKind.File, first.QuerySourceKind);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string GetFixturePath() {
        return Path.GetFullPath(Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "Tests",
            "Logs",
            "NamedFilterExamples.evtx"));
    }

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"EventViewerX-Native-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
