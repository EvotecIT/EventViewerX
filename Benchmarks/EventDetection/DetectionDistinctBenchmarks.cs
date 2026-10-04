using BenchmarkDotNet.Attributes;
using EventViewerX;
using EventViewerX.Native;

namespace EventViewerX.DetectionBenchmarks;

/// <summary>Measures duplicate bursts and distinct cardinality in a bounded correlation window.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class DetectionDistinctBenchmarks {
    private EventDetectionPlan _plan = null!;
    private EventDetectionEngineOptions _options = null!;
    private EventObservation[] _observations = null!;
    private int _enumerated;

    /// <summary>Observations accepted in one execution.</summary>
    [Params(1_000, 10_000)]
    public int EventCount { get; set; }

    /// <summary>Repeated source-address values below the rule's threshold.</summary>
    [Params(1, 1_000)]
    public int DistinctValues { get; set; }

    /// <summary>Creates deterministic input and proves complete execution outside measurement.</summary>
    [GlobalSetup]
    public void Setup() {
        _plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "BENCH-DISTINCT",
                Title = "Distinct source window",
                Kind = EventDetectionRuleKind.DistinctValue,
                EventIds = new[] { 9001 },
                Threshold = DistinctValues + 1,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                DistinctBy = "SourceAddress"
            })
        });
        _options = new EventDetectionEngineOptions(maximumObservations: 0,
            maximumGroups: 2, maximumStateObservations: EventCount + 1,
            maximumStateBytes: long.MaxValue, maximumCandidateRules: 10);
        _observations = Enumerable.Range(0, EventCount).Select(CreateObservation).ToArray();
        if (StreamDistinctWindow() != EventCount) {
            throw new InvalidOperationException("Distinct benchmark did not enumerate every observation without findings.");
        }
    }

    /// <summary>Consumes the public streaming engine and returns an enumeration/result checksum.</summary>
    [Benchmark]
    public int StreamDistinctWindow() {
        _enumerated = 0;
        int findings = 0;
        foreach (EventDetectionFinding finding in EventDetectionEngine.Stream(Observations(), _plan, _options)) {
            findings++;
        }
        return _enumerated - findings;
    }

    private IEnumerable<EventObservation> Observations() {
        foreach (EventObservation observation in _observations) {
            _enumerated++;
            yield return observation;
        }
    }

    private EventObservation CreateObservation(int index) {
        DateTime timestamp = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc).AddTicks(index);
        var metadata = new NativeEventMetadata("EventViewerX-Benchmark", null, 9001, null,
            0, 0, 0, 0, timestamp, index + 1, null, null, 1, 1,
            "Security", "benchmark-host", null, 1);
        var source = new EventObject(metadata, queriedMachine: "benchmark-host", containerLog: "Security");
        source.Data["Account"] = "benchmark-user";
        source.Data["SourceAddress"] = "source-" + (index % DistinctValues).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return EventObservation.Create(source, receivedTimeUtc: timestamp, processedTimeUtc: timestamp);
    }
}
