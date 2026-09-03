using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventTypeEngine {
    [Fact]
    public void ReadinessRestrictionKeepsOnlyTheRequestedProviderPartition() {
        IReadOnlyList<EventSourceDefinition> sources = new[] {
            new EventSourceDefinition("Application", new[] { 42 }, new[] { "Provider-A" }),
            new EventSourceDefinition("Application", new[] { 42 }, new[] { "Provider-B" }),
            new EventSourceDefinition("Application", new[] { 42 })
        };

        EventSourceDefinition restricted = Assert.Single(
            EventTypeEngine.RestrictSources(
                sources,
                "Application",
                new[] { 42 },
                new[] { "provider-a" }));

        Assert.Equal(new[] { "Provider-A" }, restricted.ProviderNames);
    }

    [Fact]
    public void ReadinessRestrictionCanSelectOnlyTheUnscopedPartition() {
        IReadOnlyList<EventSourceDefinition> sources = new[] {
            new EventSourceDefinition("Application", new[] { 42 }, new[] { "Provider-A" }),
            new EventSourceDefinition("Application", new[] { 42 })
        };

        EventSourceDefinition restricted = Assert.Single(
            EventTypeEngine.RestrictSources(
                sources,
                "Application",
                new[] { 42 },
                Array.Empty<string>()));

        Assert.Empty(restricted.ProviderNames);
    }

    [Fact]
    public void TypedChannelBatch_PreservesProviderScopeInNativeXPath() {
        var query = new EventTypeQuery(new[] { EventType.AADSyncFilterStatus });
        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(query.Types);

        EventLogBatchQuery batch = Assert.IsType<EventLogBatchQuery>(
            EventTypeEngine.CreateBatch(
                query,
                sources,
                new EventTypeQueryExecutionInfo(),
                predicateFilter: null));

        EventLogStructuredQuery structured = Assert.Single(batch.StructuredQueries);
        Assert.Contains("EventID=6952", structured.QueryXml, StringComparison.Ordinal);
        Assert.Contains("Provider[@Name='ADSync']", structured.QueryXml, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedCollectorBatch_RejectsSameIdFromUnrelatedProvider() {
        var query = new EventTypeQuery(new[] { EventType.AADSyncFilterStatus }) {
            CollectorLogName = "ForwardedEvents"
        };
        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(query.Types);
        EventLogBatchQuery batch = EventTypeEngine.CreateCollectorBatch(
            query,
            sources,
            new EventTypeQueryExecutionInfo(),
            startTime: null,
            endTime: null);
        Func<EventObject, bool> predicate = Assert.Single(batch.ChannelQueries).ManagedPredicate!;

        Assert.True(predicate(CreateEvent(6952, "ADSync", "Application")));
        Assert.False(predicate(CreateEvent(6952, "Unrelated Provider", "Application")));
    }

    [Fact]
    public void TypedProjection_RejectsProviderOutsideCatalogScope() {
        EventObject source = CreateEvent(907, "Unrelated Provider", "Application");

        EventTypeRecord? projected = EventTypeCatalog.CreateEventRule(
            source,
            new[] { EventType.SyncCompleted });

        Assert.Null(projected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedBatch_DisjointProviderReturnsNoBatch(bool useFile) {
        var query = new EventTypeQuery(new[] { EventType.AADSyncFilterStatus });
        if (useFile) {
            query.Paths = new[] { "typed-provider-disjoint.evtx" };
        }
        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(query.Types);
        EventPredicate predicate = EventPredicate.Compare(
            "ProviderName",
            EventPredicateOperator.Equal,
            "Unrelated Provider");
        predicate.IgnoreCase = false;
        EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);

        EventLogBatchQuery? batch = EventTypeEngine.CreateBatch(
            query,
            sources,
            new EventTypeQueryExecutionInfo(),
            plan.NativeFilter);

        Assert.Null(batch);
    }

    [Fact]
    public void RejectsCredentialForImplicitLocalTarget() {
        var query = new EventTypeQuery(
            new[] { EventType.OSStartup }) {
            Credential = new System.Net.NetworkCredential(
                "reader",
                "password")
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventTypeEngine.ReadAsync(query));

        Assert.Contains(
            "every event-type target is a remote computer",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCredentialForMixedLocalAndRemoteTargets() {
        var query = new EventTypeQuery(
            new[] { EventType.OSStartup }) {
            MachineNames = new string?[] {
                null,
                "remote.contoso.test"
            },
            Credential = new System.Net.NetworkCredential(
                "reader",
                "password")
        };

        Assert.Throws<ArgumentException>(
            () => EventTypeEngine.ReadAsync(query));
    }

    [Fact]
    public void AllowsCredentialWhenEveryEventTypeTargetIsRemote() {
        var query = new EventTypeQuery(
            new[] { EventType.OSStartup }) {
            MachineNames = new[] { "remote.contoso.test" },
            Credential = new System.Net.NetworkCredential(
                "reader",
                "password")
        };

        IAsyncEnumerable<EventTypeRecord> stream =
            EventTypeEngine.ReadAsync(query);

        Assert.NotNull(stream);
    }


    [Fact]
    public async Task EmptyRestrictedQueryResetsReusableExecutionInfo() {
        var executionInfo = new EventTypeQueryExecutionInfo();
        executionInfo.Reset(maxEventsScanned: 1);
        var candidateCounter =
            new EventTypeCandidateCounter(
                maxEventsScanned: 1,
                executionInfo);
        Assert.True(
            candidateCounter.TryRecordCandidate());
        executionInfo.EventsEmitted = 1;
        executionInfo.RecordTargetFailure(
            new EventLogQueryTargetFailure(
                "remote.example.test",
                "Security",
                EventLogRemoteQueryFailureKind.AccessDenied,
                "Access denied."));
        var query = new EventTypeQuery(
            new[] { EventType.ADUserLogon }) {
            SourceLogName = "EventViewerX-Missing-Channel",
            MaxCandidates = 7
        };

        await foreach (EventTypeRecord _ in
                       EventTypeEngine.ReadAsync(
                           query,
                           executionInfo)) {
            Assert.Fail(
                "A query restricted to an unrelated channel must be empty.");
        }

        Assert.Equal(0, executionInfo.EventsScanned);
        Assert.Equal(0, executionInfo.EventsEmitted);
        Assert.Equal(7, executionInfo.MaxEventsScanned);
        Assert.False(executionInfo.ScanLimitReached);
        Assert.Empty(executionInfo.TargetFailures);
    }

    [Fact]
    public void CandidateCapsRemainLocalWhenExecutionInfoIsReused() {
        var executionInfo =
            new EventTypeQueryExecutionInfo();
        executionInfo.Reset(maxEventsScanned: 1);
        var capped =
            new EventTypeCandidateCounter(
                maxEventsScanned: 1,
                executionInfo);

        Assert.True(capped.TryRecordCandidate());

        executionInfo.Reset(maxEventsScanned: 0);
        var unlimited =
            new EventTypeCandidateCounter(
                maxEventsScanned: 0,
                executionInfo);

        Assert.True(unlimited.TryRecordCandidate());
        Assert.True(unlimited.TryRecordCandidate());
        Assert.False(capped.TryRecordCandidate());
    }

    [Fact]
    public void RemoteFailureIsRecordedOnlyForTheFailedChannel() {
        var executionInfo = new EventTypeQueryExecutionInfo();
        executionInfo.Reset(maxEventsScanned: 0);
        var failure = new EventLogQueryFailure(
            source: "Security",
            machineName: "remote.example.test",
            exception: new UnauthorizedAccessException("Access denied."));

        EventTypeEngine.HandleFailure(
            failure,
            executionInfo);

        EventLogQueryTargetFailure recorded =
            Assert.Single(executionInfo.TargetFailures);
        Assert.Equal("remote.example.test", recorded.MachineName);
        Assert.Equal("Security", recorded.LogName);
        Assert.Equal(
            EventLogRemoteQueryFailureKind.AccessDenied,
            recorded.Kind);
    }

    private static EventObject CreateEvent(int eventId, string providerName, string logName) {
        var metadata = new NativeEventMetadata(
            providerName: providerName,
            providerId: null,
            id: eventId,
            qualifiers: null,
            level: 0,
            task: null,
            opcode: null,
            keywords: null,
            timeCreated: DateTime.UtcNow,
            recordId: 1,
            activityId: null,
            relatedActivityId: null,
            processId: null,
            threadId: null,
            logName: logName,
            machineName: "source.example.test",
            userId: null,
            version: null);
        return new EventObject(metadata, "collector.example.test", "ForwardedEvents");
    }
}
