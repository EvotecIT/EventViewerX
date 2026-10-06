using System.Reflection;

namespace EventViewerX.Benchmarks;

/// <summary>Exercises completion matching after a retained expired absence prefix.</summary>
public sealed class EventAbsenceBenchmarkFixture {
    private readonly EventDetectionReplaySession session;
    private readonly EventObservation[] completions;
    private readonly int count;

    /// <summary>Populates pending triggers outside the measured completion operation.</summary>
    public EventAbsenceBenchmarkFixture(int count) {
        this.count = count;
        var plan = EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "missing", Title = "Missing", Kind = EventDetectionRuleKind.Absence,
            Window = TimeSpan.FromMinutes(1), CoverageScope = "fixture",
            Steps = new[] {
                new EventDetectionStepDefinition { Name = "start", EventIds = new[] { 1 } },
                new EventDetectionStepDefinition { Name = "end", EventIds = new[] { 2 } }
            }
        }) });
        session = new EventDetectionReplaySession(plan, "fixture", "1", new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()));
        DateTime time = new(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < count; i++) { session.Process(Observation(1, time, i + 1)); }
        completions = Enumerable.Range(0, count).Select(i => Observation(2, time.AddMinutes(5), count + i + 1)).ToArray();
    }

    /// <summary>Processes late completions without discarding the original absent-trigger evidence.</summary>
    public void MatchCompletions() {
        foreach (EventObservation observation in completions) {
            if (session.Process(observation).Count != 0) { throw new InvalidOperationException("Unexpected immediate finding."); }
        }
    }

    /// <summary>Validates every expired trigger survives and no completion consumes it.</summary>
    public int Validate() {
        var findings = session.Complete();
        if (findings.Count != count || findings.Any(f => f.Status != EventDetectionFindingStatus.Incomplete)) {
            throw new InvalidOperationException("Expired absence evidence changed.");
        }
        return findings.Count;
    }

    private static EventObservation Observation(int id, DateTime time, long recordId) {
        var record = new SavedEventRecord { EventId = id, RecordId = recordId, TimeCreatedUtc = time,
            Channel = "Tasks", ProviderName = "Fixture", Computer = "fixture" };
        // Fixture construction only: use the same saved-record projection as production readers.
        var method = typeof(SavedEventRecord).GetMethod("ToEventObject", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var source = (EventObject)method.Invoke(record, new object[] { "fixture.evtx", EventReadMode.Full })!;
        return EventObservation.Create(source, receivedTimeUtc: time, processedTimeUtc: time);
    }
}
