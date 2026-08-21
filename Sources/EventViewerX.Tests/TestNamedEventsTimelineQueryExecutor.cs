using EventViewerX.Reports.Correlation;
using System.Reflection;
using Xunit;

namespace EventViewerX.Tests;

public class TestNamedEventsTimelineQueryExecutor {
    [Fact]
    public async Task TryBuildAsync_ShouldFailWhenNamedEventsMissing() {
        var (result, failure) = await NamedEventsTimelineQueryExecutor.TryBuildAsync(
            new NamedEventsTimelineQueryRequest());

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(NamedEventsTimelineQueryFailureKind.InvalidArgument, failure!.Kind);
        Assert.Contains("namedEvents", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryBuildAsync_ShouldFailWhenTimeRangeInvalid() {
        var (result, failure) = await NamedEventsTimelineQueryExecutor.TryBuildAsync(
            new NamedEventsTimelineQueryRequest {
                EventType = new[] { EventType.ADUserLogon },
                StartTimeUtc = new DateTime(2026, 2, 20, 11, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc)
            });

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(NamedEventsTimelineQueryFailureKind.InvalidArgument, failure!.Kind);
        Assert.Contains("startTimeUtc", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryBuildAsync_ShouldFailWhenTimePeriodCombinedWithRange() {
        var (result, failure) = await NamedEventsTimelineQueryExecutor.TryBuildAsync(
            new NamedEventsTimelineQueryRequest {
                EventType = new[] { EventType.ADUserLogon },
                TimePeriod = TimePeriod.Last1Hour,
                StartTimeUtc = new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc)
            });

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(NamedEventsTimelineQueryFailureKind.InvalidArgument, failure!.Kind);
        Assert.Contains("timePeriod", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryBuildAsync_ShouldFailWhenCorrelationKeyInvalid() {
        var (result, failure) = await NamedEventsTimelineQueryExecutor.TryBuildAsync(
            new NamedEventsTimelineQueryRequest {
                EventType = new[] { EventType.ADUserLogon },
                CorrelationKeys = new[] { "invalid_dimension" }
            });

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(NamedEventsTimelineQueryFailureKind.InvalidArgument, failure!.Kind);
        Assert.Contains("correlationKeys", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryBuildAsync_ShouldFailWhenEventIdsContainNonPositiveValues() {
        var (result, failure) = await NamedEventsTimelineQueryExecutor.TryBuildAsync(
            new NamedEventsTimelineQueryRequest {
                EventType = new[] { EventType.ADUserLogon },
                EventIds = new[] { 4624, 0 }
            });

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(NamedEventsTimelineQueryFailureKind.InvalidArgument, failure!.Kind);
        Assert.Contains("eventIds", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryBuildAsync_ShouldFailWhenCandidateScanLimitIsNegative() {
        var (result, failure) = await NamedEventsTimelineQueryExecutor.TryBuildAsync(
            new NamedEventsTimelineQueryRequest {
                EventType = new[] { EventType.ADUserLogon },
                MaxEventsScanned = -1
            });

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(NamedEventsTimelineQueryFailureKind.InvalidArgument, failure!.Kind);
        Assert.Contains("maxEventsScanned", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseUtcValue_ShouldTreatUnspecifiedTimestampAsUtc() {
        var parsed = NamedEventsTimelineQueryExecutor.TryParseUtcValue("2026-02-20T12:34:56", out var utc);

        Assert.True(parsed);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 2, 20, 12, 34, 56, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void TryParseUtcValue_ShouldConvertOffsetTimestampToUtc() {
        var parsed = NamedEventsTimelineQueryExecutor.TryParseUtcValue("2026-02-20T12:34:56+02:00", out var utc);

        Assert.True(parsed);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 2, 20, 10, 34, 56, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void TryParseUtcValue_ShouldReturnFalseForInvalidInput() {
        var parsed = NamedEventsTimelineQueryExecutor.TryParseUtcValue("not-a-timestamp", out _);

        Assert.False(parsed);
    }

    [Fact]
    public void ReadPayloadUtc_ShouldOmitTheMissingTimestampSentinel() {
        MethodInfo method = typeof(NamedEventsTimelineQueryExecutor).GetMethod(
            "ReadPayloadUtc",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        IReadOnlyDictionary<string, object?> payload =
            new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase) {
                ["when"] = DateTime.MinValue.ToString("O")
            };

        object? result = method.Invoke(
            null,
            new object[] {
                payload,
                "when"
            });

        Assert.Null(
            result);
    }

    [Fact]
    public void ExtractPayload_ShouldExcludeBaseMetadataAndEventSnapshots() {
        MethodInfo method = typeof(NamedEventsTimelineQueryExecutor).GetMethod(
            "ExtractPayload",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var item = (PayloadTestSlim)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PayloadTestSlim));

        var payload = Assert.IsType<Dictionary<string, object?>>(method.Invoke(null, new object[] { item }));

        Assert.Equal("alice", payload["who"]);
        Assert.DoesNotContain("event", payload.Keys);
        Assert.DoesNotContain("_event_object", payload.Keys);
        Assert.DoesNotContain("event_id", payload.Keys);
        Assert.DoesNotContain("record_id", payload.Keys);
        Assert.DoesNotContain("type", payload.Keys);
        Assert.DoesNotContain("duplicate_event", payload.Keys);
    }

    [Fact]
    public void BuildCorrelationToken_ShouldNotCollideWhenValuesContainLegacySeparators() {
        MethodInfo method = typeof(NamedEventsTimelineQueryExecutor).GetMethod(
            "BuildCorrelationToken",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var left = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["action"] = "read|who=alice",
            ["who"] = "server"
        };
        var right = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["action"] = "read",
            ["who"] = "alice|who=server"
        };

        string leftToken = Assert.IsType<string>(method.Invoke(null, new object[] { left }));
        string rightToken = Assert.IsType<string>(method.Invoke(null, new object[] { right }));

        Assert.NotEqual(leftToken, rightToken);
    }

    [Fact]
    public void BuildCorrelationToken_ShouldDistinguishBlankFromLiteralEmptyMarker() {
        MethodInfo method = typeof(NamedEventsTimelineQueryExecutor).GetMethod(
            "BuildCorrelationToken",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var blank = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["who"] = string.Empty
        };
        var literal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["who"] = "<empty>"
        };

        string blankToken = Assert.IsType<string>(method.Invoke(null, new object[] { blank }));
        string literalToken = Assert.IsType<string>(method.Invoke(null, new object[] { literal }));

        Assert.NotEqual(blankToken, literalToken);
    }

    [Fact]
    public void ApplyTargetFailuresMarksPartialReportsIncompleteAndTruncated() {
        var queryInfo = new EventTypeQueryExecutionInfo();
        queryInfo.Reset(maxEventsScanned: 0);
        queryInfo.RecordTargetFailure(
            new EventLogQueryTargetFailure("AD2", "System", EventLogRemoteQueryFailureKind.HostUnavailable, "offline"));
        var result = new NamedEventsTimelineQueryResult();

        NamedEventsTimelineQueryExecutor.ApplyTargetFailures(result, queryInfo);

        Assert.True(result.Incomplete);
        Assert.True(result.Truncated);
        EventLogQueryTargetFailure failure = Assert.Single(result.TargetFailures);
        Assert.Equal("AD2", failure.MachineName);
        Assert.Equal("System", failure.LogName);
        Assert.Equal(EventLogRemoteQueryFailureKind.HostUnavailable, failure.Kind);
    }

    private sealed class PayloadTestSlim : EventTypeRecord {
        private PayloadTestSlim(EventObject eventObject) : base(eventObject) {
        }

        public string Who => "alice";
        public EventObject? DuplicateEvent => null;
    }
}
