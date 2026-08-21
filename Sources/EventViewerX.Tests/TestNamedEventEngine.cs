using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventTypeEngine {
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
}
