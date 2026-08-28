using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using EventViewerX;
using EventViewerX.Native;

namespace EventViewerX.DetectionBenchmarks;

/// <summary>Measures indexed detection-plan scaling as total enabled rules grow.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[OperationsPerSecond]
public class DetectionCandidateIndexBenchmarks {
    private EventDetectionPlan _plan = null!;
    private EventObservation[] _observations = null!;

    /// <summary>Total enabled rules. Only one indexed candidate applies to each observation.</summary>
    [Params(1, 10, 100, 1000)]
    public int RuleCount { get; set; }

    /// <summary>Builds a deterministic plan and observation set outside the measurement.</summary>
    [GlobalSetup]
    public void Setup() {
        IEventDetectionRule[] rules = Enumerable.Range(0, RuleCount)
            .Select(index => (IEventDetectionRule)new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = $"BENCH-{index:D4}",
                Version = "1.0.0",
                Title = "Detection benchmark rule " + index,
                Kind = EventDetectionRuleKind.Stateless,
                EventIds = new[] { 10000 + index },
                Channels = new[] { "Security" }
            }))
            .ToArray();
        _plan = EventDetectionPlan.Compile(rules);
        _observations = Enumerable.Range(0, 1000)
            .Select(index => CreateObservation(10000 + index % RuleCount, index + 1))
            .ToArray();
    }

    /// <summary>Evaluates 1,000 observations and returns the materialized result.</summary>
    [Benchmark]
    public EventDetectionExecutionResult EvaluateIndexedCandidates() =>
        EventDetectionEngine.Evaluate(_observations, _plan);

    private static EventObservation CreateObservation(int eventId, long recordId) {
        DateTime timestamp = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc).AddSeconds(recordId);
        var metadata = new NativeEventMetadata(
            "EventViewerX-Benchmark",
            providerId: null,
            eventId,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            timestamp,
            recordId,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 1,
            logName: "Security",
            machineName: "benchmark-host",
            userId: null,
            version: 1);
        var source = new EventObject(metadata, queriedMachine: "benchmark-host", containerLog: "Security");
        return EventObservation.Create(source, receivedTimeUtc: timestamp, processedTimeUtc: timestamp);
    }
}
