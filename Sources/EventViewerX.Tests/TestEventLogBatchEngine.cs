using Xunit;

namespace EventViewerX.Tests;

    public sealed class TestEventLogBatchEngine {
    [Fact]
    public void CombinePreservesCompatibleBatchControls() {
        Action<EventLogQueryFailure> handler =
            _ => { };
        EventLogBatchQuery first =
            EventLogBatchQuery.ForChannels(
                new[] {
                    new EventLogChannelQuery(
                        "Application")
                });
        EventLogBatchQuery second =
            EventLogBatchQuery.ForFiles(
                new[] {
                    new EventLogFileQuery(
                        "missing.evtx")
                });
        foreach (EventLogBatchQuery batch in
                 new[] { first, second }) {
            batch.MaxEvents = 7;
            batch.MaxConcurrency = 3;
            batch.ContinueOnError = true;
            batch.FailureHandler = handler;
        }

        EventLogBatchQuery combined =
            EventLogBatchQuery.Combine(
                new[] { first, second });

        Assert.Equal(7, combined.MaxEvents);
        Assert.Equal(3, combined.MaxConcurrency);
        Assert.True(combined.ContinueOnError);
        Assert.Equal(
            handler,
            combined.FailureHandler);
    }

    [Fact]
    public void CombineRejectsConflictingBatchControls() {
        EventLogBatchQuery first =
            EventLogBatchQuery.ForChannels(
                new[] {
                    new EventLogChannelQuery(
                        "Application")
                });
        EventLogBatchQuery second =
            EventLogBatchQuery.ForFiles(
                new[] {
                    new EventLogFileQuery(
                        "missing.evtx")
                });
        first.MaxEvents = 5;
        second.MaxEvents = 0;

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EventLogBatchQuery.Combine(
                    new[] { first, second }));

        Assert.Contains(
            "same MaxEvents",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFactoryPartitionsAndConsolidatesLargeFilters() {
        EventLogBatchQuery query =
            EventLogQueryFactory.ForChannels(
                new[] { "System", "Application" },
                new string?[] { null },
                new EventFilter {
                    EventIds = Enumerable.Range(1, 50)
                        .ToArray()
                },
                new EventLogQueryOptions {
                    MaxEvents = 7,
                    MaxConcurrency = 3,
                    ReadMode = EventReadMode.Metadata
                });

        EventLogStructuredQuery structured =
            Assert.Single(
                query.StructuredQueries);
        Assert.Equal(7, query.MaxEvents);
        Assert.Equal(3, query.MaxConcurrency);
        Assert.Contains("System", structured.QueryXml);
        Assert.Contains("Application", structured.QueryXml);
        Assert.True(
            structured.QueryXml
                .Split("<Select", StringSplitOptions.None)
                .Length > 3);
    }

    [Fact]
    public void QueryFactoryUsesStructuredSuppressionForExcludedNamedData() {
        EventLogBatchQuery query =
            EventLogQueryFactory.ForFiles(
                new[] { GetFixturePath() },
                new EventFilter {
                    EventIds = new[] { 7040 },
                    ExcludedNamedData =
                        new Dictionary<string, IReadOnlyList<string>> {
                            ["param4"] = new[] { "BITS" }
                        }
                });

        EventLogStructuredQuery structured =
            Assert.Single(query.StructuredQueries);
        Assert.Empty(query.FileQueries);
        Assert.Contains(
            "EventID=7040",
            structured.QueryXml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Suppress",
            structured.QueryXml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Data[@Name='param4'] = 'BITS'",
            structured.QueryXml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFactoryRejectsUnrepresentableNamedDataSuppressions() {
        var excluded =
            Enumerable.Range(1, 12)
                .ToDictionary(
                    index => "Field" + index,
                    index =>
                        (IReadOnlyList<string>)new[] {
                            "Value" + index
                        });

        Assert.Throws<ArgumentException>(() =>
            EventLogQueryFactory.ForFiles(
                new[] { GetFixturePath() },
                new EventFilter {
                    ExcludedNamedData = excluded
                }));
        Assert.Throws<ArgumentException>(() =>
            EventLogQueryFactory.ForChannels(
                new[] { "Application" },
                filter: new EventFilter {
                    ExcludedNamedData = excluded
                }));
    }

    [Fact]
    public void QueryFactoryPartitionsLargePositiveNamedDataFilters() {
        var filter = new EventFilter {
            NamedData =
                new Dictionary<string, IReadOnlyList<string>> {
                    ["Field"] = Enumerable.Range(1, 12)
                        .Select(index => "Value" + index)
                        .ToArray()
                }
        };

        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(filter);
        EventLogBatchQuery fileQuery =
            EventLogQueryFactory.ForFiles(
                new[] { GetFixturePath() },
                filter);
        EventLogBatchQuery channelQuery =
            EventLogQueryFactory.ForChannels(
                new[] { "Application" },
                filter: filter);

        Assert.Equal(2, partitions.Count);
        Assert.All(
            partitions,
            static partition =>
                Assert.InRange(
                    EventFilterCompiler.CountExpressions(
                        partition),
                    1,
                    EventFilterCompiler.MaximumXPathExpressions));
        Assert.True(
            fileQuery.FileQueries.Count +
            fileQuery.StructuredQueries.Count > 0);
        Assert.True(
            channelQuery.ChannelQueries.Count +
            channelQuery.StructuredQueries.Count > 0);
    }

    [Fact]
    public void StructuredBatchExpansionExposesEveryIndependentFileSource() {
        EventLogStructuredQuery source =
            EventLogStructuredQuery.ForFiles(
                new[] {
                    Path.Combine(
                        Path.GetTempPath(),
                        "first.evtx"),
                    Path.Combine(
                        Path.GetTempPath(),
                        "second.evtx")
                });

        IReadOnlyList<EventLogStructuredQuery> expanded =
            EventLogBatchEngine.ExpandStructuredSources(
                source);

        Assert.Equal(2, expanded.Count);
        Assert.All(
            expanded,
            static query =>
                Assert.Equal(
                    1,
                    query.GetIndependentSourceCount()));
    }

    [Fact]
    public async Task AsyncBatchMatchesSynchronousNativeSelection() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();

        var syncQuery = EventLogBatchQuery.ForFiles(new[] {
            new EventLogFileQuery(path) {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 4
            }
        });
        syncQuery.MaxEvents = 4;
        long[] expected = EventLogBatchEngine.Read(syncQuery)
            .Select(static eventObject =>
                eventObject.RecordId ?? 0)
            .ToArray();

        var asyncQuery = EventLogBatchQuery.ForFiles(new[] {
            new EventLogFileQuery(path) {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 4
            }
        });
        asyncQuery.MaxEvents = 4;
        asyncQuery.MaxConcurrency = 2;
        var actual = new List<long>();
        await foreach (EventObject eventObject in
                       EventLogBatchEngine.ReadAsync(asyncQuery)) {
            actual.Add(eventObject.RecordId ?? 0);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SynchronousBatchSnapshotsControlsAndSourcesAtReadCall() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        var source = new EventLogFileQuery(path) {
            ReadMode = EventReadMode.Metadata
        };
        EventLogBatchQuery query =
            EventLogBatchQuery.ForFiles(
                new[] { source });
        query.MaxEvents = 1;
        query.MaxConcurrency = 1;

        IEnumerable<EventObject> stream =
            EventLogBatchEngine.Read(query);
        source.XPath = "*[";
        query.MaxEvents = 0;
        query.MaxConcurrency = 0;

        Assert.Single(stream);
    }

    [Fact]
    public async Task AsyncBatchSnapshotsControlsAndSourcesAtReadCall() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        var source = new EventLogFileQuery(path) {
            ReadMode = EventReadMode.Metadata
        };
        EventLogBatchQuery query =
            EventLogBatchQuery.ForFiles(
                new[] { source });
        query.MaxEvents = 1;
        query.MaxConcurrency = 1;

        IAsyncEnumerable<EventObject> stream =
            EventLogBatchEngine.ReadAsync(query);
        source.XPath = "*[";
        query.MaxEvents = 0;
        query.MaxConcurrency = 0;
        var events = new List<EventObject>();
        await foreach (EventObject eventObject in stream) {
            events.Add(eventObject);
        }

        Assert.Single(events);
    }

    [Fact]
    public async Task AsyncPrimingFailureReleasesSuccessfulSourceCursors() {
        if (!OperatingSystem.IsWindows()) return;
        string root = Path.Combine(
            Path.GetTempPath(),
            $"EventViewerX-BatchCleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string validPath = Path.Combine(
            root,
            "valid.evtx");
        string missingPath = Path.Combine(
            root,
            "missing.evtx");
        File.Copy(
            GetFixturePath(),
            validPath);
        try {
            var query = EventLogBatchQuery.ForFiles(new[] {
                new EventLogFileQuery(validPath) {
                    ReadMode = EventReadMode.Metadata
                },
                new EventLogFileQuery(missingPath) {
                    ReadMode = EventReadMode.Metadata
                }
            });
            query.MaxConcurrency = 2;

            await Assert.ThrowsAsync<FileNotFoundException>(
                async () => {
                    await foreach (EventObject _ in
                                   EventLogBatchEngine.ReadAsync(
                                       query)) {
                    }
                });

            File.Delete(validPath);
            Assert.False(File.Exists(validPath));
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void ConsolidatedFileBatchDeduplicatesOverlappingNativeSelects() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        int eventId = EventLogEngine.ReadFile(
                new EventLogFileQuery(path) {
                    MaxEvents = 1,
                    ReadMode = EventReadMode.Metadata
                })
            .Single()
            .Id;

        EventLogFileQuery[] sources = Enumerable.Range(0, 2)
            .Select(_ => new EventLogFileQuery(path) {
                XPath = $"*[System[EventID={eventId}]]",
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 16
            })
            .ToArray();
        var query = EventLogBatchQuery.ForFiles(sources);
        query.MaxEvents = 20;
        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(query);
        EventLogStructuredQuery structured =
            Assert.Single(consolidated.StructuredQueries);
        Assert.Single(
            System.Xml.Linq.XDocument
                .Parse(structured.QueryXml)
                .Descendants("Select"));

        EventObject[] actual =
            EventLogBatchEngine.Read(consolidated).ToArray();

        Assert.NotEmpty(actual);
        Assert.True(actual.Length <= 16);
        Assert.Equal(
            actual.Length,
            actual
                .Select(static item => item.RecordId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void ConsolidationIgnoresOperationalQueryIdsWhenDeduplicating() {
        const string firstQuery =
            "<QueryList><Query Id=\"1\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query></QueryList>";
        const string secondQuery =
            "<QueryList><Query Id=\"99\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query></QueryList>";
        EventLogBatchQuery query =
            EventLogBatchQuery.ForStructured(
                new[] {
                    new EventLogStructuredQuery(firstQuery),
                    new EventLogStructuredQuery(secondQuery)
                });

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(query);
        EventLogStructuredQuery structured =
            Assert.Single(
                consolidated.StructuredQueries);
        System.Xml.Linq.XDocument document =
            System.Xml.Linq.XDocument.Parse(
                structured.QueryXml);

        System.Xml.Linq.XElement queryElement =
            Assert.Single(
                document.Descendants("Query"));
        Assert.Equal(
            "0",
            (string?)queryElement.Attribute("Id"));
        Assert.Single(
            queryElement.Elements("Select"));
    }

    [Fact]
    public void ConsolidationPreservesEachBoundedChannelSource() {
        EventLogChannelQuery[] sources = {
            new("Application") {
                MaxEvents = 10,
                ReadMode = EventReadMode.Metadata
            },
            new("System") {
                MaxEvents = 10,
                ReadMode = EventReadMode.Metadata
            }
        };

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(
                EventLogBatchQuery.ForChannels(
                    sources));

        Assert.Equal(
            2,
            consolidated.StructuredQueries.Count);
        Assert.All(
            consolidated.StructuredQueries,
            static query =>
                Assert.Equal(10, query.MaxEvents));
        Assert.Contains(
            consolidated.StructuredQueries,
            static query =>
                query.QueryXml.Contains(
                    "Application",
                    StringComparison.Ordinal));
        Assert.Contains(
            consolidated.StructuredQueries,
            static query =>
                query.QueryXml.Contains(
                    "System",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void ConsolidationPreservesEachBoundedFilterOnTheSameChannel() {
        EventLogChannelQuery[] sources = {
            new("Application") {
                XPath = "*[System[EventID=1000]]",
                MaxEvents = 10,
                ReadMode = EventReadMode.Metadata
            },
            new("Application") {
                XPath = "*[System[EventID=1001]]",
                MaxEvents = 10,
                ReadMode = EventReadMode.Metadata
            }
        };

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(
                EventLogBatchQuery.ForChannels(
                    sources));

        Assert.Equal(
            2,
            consolidated.StructuredQueries.Count);
        Assert.All(
            consolidated.StructuredQueries,
            static query =>
                Assert.Equal(10, query.MaxEvents));
    }

    [Fact]
    public void ConsolidationKeepsBookmarkPartitionsOnOneNativeSource() {
        const string bookmark =
            "<BookmarkList><Bookmark Channel=\"Application\" RecordId=\"1\" IsCurrent=\"true\" /></BookmarkList>";
        EventLogChannelQuery[] sources = {
            new("Application") {
                XPath = "*[System[EventID=1000]]",
                MaxEvents = 10,
                BookmarkXml = bookmark,
                BatchSourceIdentity = "partitioned-filter",
                ReadMode = EventReadMode.Metadata
            },
            new("Application") {
                XPath = "*[System[EventID=1001]]",
                MaxEvents = 10,
                BookmarkXml = bookmark,
                BatchSourceIdentity = "partitioned-filter",
                ReadMode = EventReadMode.Metadata
            }
        };

        EventLogStructuredQuery consolidated =
            Assert.Single(
                EventLogBatchConsolidator
                    .Consolidate(
                        EventLogBatchQuery.ForChannels(
                            sources))
                    .StructuredQueries);
        Assert.Equal(
            2,
            System.Xml.Linq.XDocument
                .Parse(consolidated.QueryXml)
                .Descendants("Select")
                .Count());
    }

    [Fact]
    public void ConsolidationPreservesIndependentBookmarkedFilters() {
        const string bookmark =
            "<BookmarkList><Bookmark Channel=\"Application\" RecordId=\"1\" IsCurrent=\"true\" /></BookmarkList>";
        EventLogChannelQuery[] sources = {
            new("Application") {
                XPath = "*[System[EventID=1000]]",
                MaxEvents = 10,
                BookmarkXml = bookmark,
                ReadMode = EventReadMode.Metadata
            },
            new("Application") {
                XPath = "*[System[EventID=1001]]",
                MaxEvents = 10,
                BookmarkXml = bookmark,
                ReadMode = EventReadMode.Metadata
            }
        };

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(
                EventLogBatchQuery.ForChannels(
                    sources));

        Assert.Equal(
            2,
            consolidated.StructuredQueries.Count);
        Assert.All(
            consolidated.StructuredQueries,
            static query =>
                Assert.Equal(10, query.MaxEvents));
    }

    [Fact]
    public void ConsolidationClonesRemoteCredentials() {
        var credential =
            new System.Net.NetworkCredential(
                "before",
                "secret",
                "domain");
        var source =
            new EventLogChannelQuery(
                "Application") {
                MachineName = "remote.example.test",
                Credential = credential
            };

        EventLogStructuredQuery consolidated =
            Assert.Single(
                EventLogBatchConsolidator
                    .Consolidate(
                        EventLogBatchQuery.ForChannels(
                            new[] { source }))
                    .StructuredQueries);
        credential.UserName = "after";
        credential.Password = "changed";
        credential.Domain = "other";

        Assert.NotSame(
            credential,
            consolidated.Credential);
        Assert.Equal(
            "before",
            consolidated.Credential!.UserName);
        Assert.Equal(
            "secret",
            consolidated.Credential.Password);
        Assert.Equal(
            "domain",
            consolidated.Credential.Domain);
    }

    [Fact]
    public void ConsolidationKeepsChannelAndFileQueriesOnSeparateNativeHandles() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        var channel = new EventLogChannelQuery("System") {
            XPath = "*[System[EventID=7040]]",
            ReadMode = EventReadMode.Metadata,
            MaxEvents = 3
        };
        var file = new EventLogFileQuery(path) {
            XPath = "*[System[EventID=7040]]",
            ReadMode = EventReadMode.Metadata,
            MaxEvents = 3
        };
        EventLogBatchQuery combined = EventLogBatchQuery.Combine(new[] {
            EventLogBatchQuery.ForChannels(new[] { channel }),
            EventLogBatchQuery.ForFiles(new[] { file })
        });

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(combined);

        Assert.Equal(2, consolidated.StructuredQueries.Count);
        Assert.Contains(
            consolidated.StructuredQueries,
            static query =>
                query.SourceKind == EventLogQuerySourceKind.Channel);
        Assert.Contains(
            consolidated.StructuredQueries,
            static query =>
                query.SourceKind == EventLogQuerySourceKind.File);
        EventObject[] events =
            EventLogBatchEngine.Read(consolidated).ToArray();
        Assert.Contains(
            events,
            item => string.Equals(
                item.GatheredFrom,
                path,
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            events,
            item => string.Equals(
                item.ContainerLog,
                "System",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConsolidationPreservesFileSuppressExpressions() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        var select = new EventFilter {
            EventIds = new[] { 7040 }
        };
        var suppress = new EventFilter {
            ProviderNames = new[] { "Service Control Manager" }
        };
        var structured = new EventLogStructuredQuery(
            EventFilterCompiler.BuildFileQueryXml(
                new[] { path },
                select,
                suppress)) {
            SourceKind = EventLogQuerySourceKind.File,
            ReadMode = EventReadMode.Metadata
        };

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(
                EventLogBatchQuery.ForStructured(
                    new[] { structured }));

        Assert.Empty(EventLogBatchEngine.Read(consolidated));
    }

    [Fact]
    public void ConsolidationEscapesReservedOfflinePathCharacters() {
        string path = Path.GetFullPath(
            Path.Combine(
                "logs",
                "batch#source?.evtx"));
        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(
                EventLogBatchQuery.ForFiles(
                    new[] {
                        new EventLogFileQuery(path)
                    }));
        EventLogStructuredQuery structured =
            Assert.Single(
                consolidated.StructuredQueries);
        EventLogStructuredQuerySource source =
            Assert.Single(
                structured.ResolveSources());

        Assert.Equal(path, source.Source);
        Assert.Contains(
            "%23",
            structured.QueryXml,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "%3F",
            structured.QueryXml,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsolidationKeepsStructuredErrorPoliciesOnSeparateNativeHandles() {
        const string firstQuery =
            "<QueryList><Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query></QueryList>";
        const string secondQuery =
            "<QueryList><Query Id=\"1\" Path=\"Application\">" +
            "<Select Path=\"Application\">*</Select>" +
            "</Query></QueryList>";
        Action<EventLogQueryFailure> firstHandler =
            static _ => { };
        Action<EventLogQueryFailure> secondHandler =
            static _ => { };
        EventLogStructuredQuery[] sources = {
            new EventLogStructuredQuery(firstQuery) {
                TolerateQueryErrors = false
            },
            new EventLogStructuredQuery(secondQuery) {
                TolerateQueryErrors = true,
                FailureHandler = firstHandler
            },
            new EventLogStructuredQuery(secondQuery) {
                TolerateQueryErrors = true,
                FailureHandler = secondHandler
            }
        };

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(
                EventLogBatchQuery.ForStructured(
                    sources));

        Assert.Equal(
            3,
            consolidated.StructuredQueries.Count);
        Assert.Contains(
            consolidated.StructuredQueries,
            static query =>
                !query.TolerateQueryErrors &&
                query.FailureHandler == null);
        Assert.Contains(
            consolidated.StructuredQueries,
            query =>
                query.TolerateQueryErrors &&
                query.FailureHandler == firstHandler);
        Assert.Contains(
            consolidated.StructuredQueries,
            query =>
                query.TolerateQueryErrors &&
                query.FailureHandler == secondHandler);
    }

    [Fact]
    public void ContinueOnErrorReportsOneSourceAndKeepsValidEvents() {
        if (!OperatingSystem.IsWindows()) return;
        string path = GetFixturePath();
        string missing = Path.Combine(
            Path.GetTempPath(),
            $"missing-{Guid.NewGuid():N}.evtx");
        var failures = new List<EventLogQueryFailure>();
        var query = EventLogBatchQuery.ForFiles(new[] {
            new EventLogFileQuery(path) {
                MaxEvents = 2,
                ReadMode = EventReadMode.Metadata
            },
            new EventLogFileQuery(missing) {
                MaxEvents = 2,
                ReadMode = EventReadMode.Metadata
            }
        });
        query.ContinueOnError = true;
        query.FailureHandler = failures.Add;

        EventObject[] actual = EventLogBatchEngine.Read(query).ToArray();

        Assert.Equal(2, actual.Length);
        EventLogQueryFailure failure = Assert.Single(failures);
        Assert.Equal(missing, failure.Source);
        Assert.IsType<FileNotFoundException>(failure.Exception);
    }

    [Fact]
    public void ParallelPrimingSerializesFailureHandlerCalls() {
        int active = 0;
        int maximumActive = 0;
        int failures = 0;
        EventLogBatchQuery query =
            CreateMissingFileBatch();
        query.FailureHandler = _ => {
            int current =
                Interlocked.Increment(
                    ref active);
            UpdateMaximum(
                ref maximumActive,
                current);
            Thread.Sleep(25);
            Interlocked.Increment(
                ref failures);
            Interlocked.Decrement(
                ref active);
        };

        Assert.Empty(
            EventLogBatchEngine.Read(
                query));

        Assert.Equal(8, failures);
        Assert.Equal(1, maximumActive);
    }

    [Fact]
    public async Task AsyncParallelPrimingSerializesFailureHandlerCalls() {
        int active = 0;
        int maximumActive = 0;
        int failures = 0;
        EventLogBatchQuery query =
            CreateMissingFileBatch();
        query.FailureHandler = _ => {
            int current =
                Interlocked.Increment(
                    ref active);
            UpdateMaximum(
                ref maximumActive,
                current);
            Thread.Sleep(25);
            Interlocked.Increment(
                ref failures);
            Interlocked.Decrement(
                ref active);
        };

        await foreach (EventObject _ in
                       EventLogBatchEngine.ReadAsync(
                           query)) {
        }

        Assert.Equal(8, failures);
        Assert.Equal(1, maximumActive);
    }

    [Fact]
    public void FatalSynchronousPrimerCancelsItsSibling() {
        using var siblingStarted =
            new ManualResetEventSlim();
        var stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                EventLogBatchEngine
                    .PrimeConcurrently<PrimerResource>(
                        sourceCount: 2,
                        maxConcurrency: 2,
                        CancellationToken.None,
                        (index, cancellationToken) => {
                            if (index == 0) {
                                Assert.True(
                                    siblingStarted.Wait(
                                        TimeSpan.FromSeconds(5)));
                                throw new InvalidOperationException(
                                    "fatal source");
                            }
                            siblingStarted.Set();
                            Assert.True(
                                cancellationToken.WaitHandle.WaitOne(
                                    TimeSpan.FromSeconds(5)));
                            cancellationToken
                                .ThrowIfCancellationRequested();
                            return new PrimerResource();
                        }));

        Assert.Equal(
            "fatal source",
            exception.Message);
        Assert.True(
            stopwatch.Elapsed <
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FatalAsynchronousPrimerCancelsItsSibling() {
        var siblingStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await EventLogBatchEngine
                        .PrimeConcurrentlyAsync<PrimerResource>(
                            sourceCount: 2,
                            maxConcurrency: 2,
                            CancellationToken.None,
                            async (index, cancellationToken) => {
                                if (index == 0) {
                                    await siblingStarted.Task;
                                    throw new InvalidOperationException(
                                        "fatal source");
                                }
                                siblingStarted.SetResult(true);
                                await Task.Delay(
                                    TimeSpan.FromSeconds(30),
                                    cancellationToken);
                                return new PrimerResource();
                            }));

        Assert.Equal(
            "fatal source",
            exception.Message);
        Assert.True(
            stopwatch.Elapsed <
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AsyncMoveCancellationDetachesStalledProjectionCleanup() {
        var resource = new PrimerResource();
        var moveNext =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();

        (bool completed, bool hasNext) =
            await EventLogBatchEngine
                .AwaitMoveNextAsync(
                    moveNext.Task,
                    resource,
                    cancellation.Token);

        Assert.False(completed);
        Assert.False(hasNext);
        Assert.False(resource.Disposed);

        moveNext.SetResult(true);
        Assert.True(
            SpinWait.SpinUntil(
                () => resource.Disposed,
                TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task AsyncPrimerCancellationDetachesStalledProjectionCleanup() {
        var primerEntered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePrimer =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var resource = new PrimerResource();
        using var cancellation =
            new CancellationTokenSource();

        Task<PrimerResource?[]> priming =
            EventLogBatchEngine
                .PrimeConcurrentlyAsync<PrimerResource>(
                    sourceCount: 1,
                    maxConcurrency: 1,
                    cancellation.Token,
                    async (_, _) => {
                        primerEntered.SetResult(true);
                        await releasePrimer.Task;
                        return resource;
                    });
        await primerEntered.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await priming.WaitAsync(
                TimeSpan.FromSeconds(5)));
        Assert.False(resource.Disposed);

        releasePrimer.SetResult(true);
        Assert.True(
            SpinWait.SpinUntil(
                () => resource.Disposed,
                TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void CancellationStopsTheMergedBatchEnumeration() {
        if (!OperatingSystem.IsWindows()) return;
        var query = EventLogBatchQuery.ForFiles(new[] {
            new EventLogFileQuery(GetFixturePath()) {
                Oldest = true,
                ReadMode = EventReadMode.Metadata
            }
        });
        using var cancellation = new CancellationTokenSource();
        int count = 0;

        Assert.Throws<OperationCanceledException>(() => {
            foreach (EventObject _ in EventLogBatchEngine.Read(
                         query,
                         cancellation.Token)) {
                count++;
                if (count == 3) {
                    cancellation.Cancel();
                }
            }
        });

        Assert.Equal(3, count);
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

    private static EventLogBatchQuery CreateMissingFileBatch() {
        EventLogBatchQuery query =
            EventLogBatchQuery.ForFiles(
                Enumerable.Range(0, 8)
                    .Select(_ =>
                        new EventLogFileQuery(
                            Path.Combine(
                                Path.GetTempPath(),
                                $"missing-{Guid.NewGuid():N}.evtx")) {
                            ReadMode =
                                EventReadMode.Metadata
                        })
                    .ToArray());
        query.MaxConcurrency = 8;
        query.ContinueOnError = true;
        return query;
    }

    private static void UpdateMaximum(
        ref int maximum,
        int candidate) {

        while (true) {
            int observed =
                Volatile.Read(
                    ref maximum);
            if (candidate <= observed ||
                Interlocked.CompareExchange(
                    ref maximum,
                    candidate,
                    observed) == observed) {
                return;
            }
        }
    }

    private sealed class PrimerResource : IDisposable {
        internal bool Disposed { get; private set; }

        public void Dispose() {
            Disposed = true;
        }
    }
}
