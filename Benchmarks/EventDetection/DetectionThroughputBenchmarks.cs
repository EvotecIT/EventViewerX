using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using EventViewerX;
using EventViewerX.Native;

namespace EventViewerX.DetectionBenchmarks;

/// <summary>Detection lanes used by the permanent scale matrix.</summary>
public enum DetectionBenchmarkLane {
    /// <summary>Indexed stateless selection with a compiled predicate rejection.</summary>
    StatelessPredicate,
    /// <summary>One bounded threshold window that retains candidates without producing output.</summary>
    ThresholdWindow,
    /// <summary>One ordered temporal state whose second step is intentionally absent.</summary>
    OrderedTemporal
}

/// <summary>Measures streaming detection at 1K through 1M observations without materializing the input.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[OperationsPerSecond]
public class DetectionThroughputBenchmarks {
    private EventDetectionPlan _plan = null!;
    private EventDetectionEngineOptions _options = null!;
    private EventObservation _observation = null!;
    private int _enumerated;

    /// <summary>Number of observations streamed through one execution.</summary>
    [Params(1_000, 10_000, 100_000, 1_000_000)]
    public int EventCount { get; set; }

    /// <summary>Detection behavior measured independently.</summary>
    [ParamsAllValues]
    public DetectionBenchmarkLane Lane { get; set; }

    /// <summary>Builds and proves the exact workload before BenchmarkDotNet measures it.</summary>
    [GlobalSetup]
    public void Setup() {
        _observation = CreateObservation();
        _plan = EventDetectionPlan.Compile(new[] { CreateRule() });
        _options = new EventDetectionEngineOptions(
            maximumObservations: 0,
            maximumGroups: 2,
            maximumStateObservations: EventCount + 1,
            maximumStateBytes: long.MaxValue,
            maximumCandidateRules: 10);
        int checksum = Run();
        if (_enumerated != EventCount || checksum != EventCount) {
            throw new InvalidOperationException(
                $"Detection benchmark proof failed for {Lane}/{EventCount}: enumerated={_enumerated}, checksum={checksum}.");
        }
    }

    /// <summary>Streams the selected lane and returns a checksum proving full source enumeration and zero unexpected findings.</summary>
    [Benchmark]
    public int StreamDetection() => Run();

    private int Run() {
        _enumerated = 0;
        int findings = 0;
        foreach (EventDetectionFinding finding in EventDetectionEngine.Stream(Observations(), _plan, _options)) {
            findings++;
            if (finding.Status != EventDetectionFindingStatus.Matched) {
                findings += 1_000_000;
            }
        }
        return _enumerated - findings;
    }

    private IEnumerable<EventObservation> Observations() {
        for (int index = 0; index < EventCount; index++) {
            _enumerated++;
            yield return _observation;
        }
    }

    private IEventDetectionRule CreateRule() {
        EventDetectionRuleDefinition definition = Lane switch {
            DetectionBenchmarkLane.StatelessPredicate => new EventDetectionRuleDefinition {
                RuleId = "BENCH-STATELESS",
                Title = "Stateless predicate lane",
                EventIds = new[] { 9001 },
                Channels = new[] { "Security" },
                Predicate = EventPredicate.Compare("Account", EventPredicateOperator.Equal, "never-match")
            },
            DetectionBenchmarkLane.ThresholdWindow => new EventDetectionRuleDefinition {
                RuleId = "BENCH-THRESHOLD",
                Title = "Threshold window lane",
                Kind = EventDetectionRuleKind.Threshold,
                EventIds = new[] { 9001 },
                Channels = new[] { "Security" },
                Threshold = EventCount + 1,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account"
            },
            DetectionBenchmarkLane.OrderedTemporal => new EventDetectionRuleDefinition {
                RuleId = "BENCH-TEMPORAL",
                Title = "Ordered temporal lane",
                Kind = EventDetectionRuleKind.OrderedTemporal,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                Steps = new[] {
                    new EventDetectionStepDefinition { Name = "first", EventIds = new[] { 9001 } },
                    new EventDetectionStepDefinition { Name = "second", EventIds = new[] { 9002 } }
                }
            },
            _ => throw new ArgumentOutOfRangeException()
        };
        return new EventDetectionRule(definition);
    }

    private static EventObservation CreateObservation() {
        DateTime timestamp = new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);
        var metadata = new NativeEventMetadata(
            "EventViewerX-Benchmark",
            providerId: null,
            id: 9001,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            timestamp,
            recordId: 1,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 1,
            logName: "Security",
            machineName: "benchmark-host",
            userId: null,
            version: 1);
        var source = new EventObject(metadata, queriedMachine: "benchmark-host", containerLog: "Security");
        source.Data["Account"] = "EVOTEC\\benchmark-user";
        return EventObservation.Create(source, receivedTimeUtc: timestamp, processedTimeUtc: timestamp);
    }
}
