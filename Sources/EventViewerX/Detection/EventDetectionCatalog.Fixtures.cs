using EventViewerX.Native;

namespace EventViewerX;

public static partial class EventDetectionCatalog {
    private static IEnumerable<EventDetectionFixture> CreateFixtures(EventDetectionPack pack) {
        foreach (EventDetectionRuleDefinition rule in pack.Rules) {
            yield return Fixture(pack, rule, EventDetectionFixtureKind.Positive, CreateMatchingObservations(rule),
                new[] { rule.RuleId }, "Representative activity must match.");
            yield return Fixture(pack, rule, EventDetectionFixtureKind.Negative, CreateNegativeObservations(rule),
                Array.Empty<string>(), "Insufficient or unrelated activity must not match.");
            yield return Fixture(pack, rule, EventDetectionFixtureKind.Boundary, CreateBoundaryObservations(rule),
                new[] { rule.RuleId }, "The exact declared count or time boundary must match deterministically.");
            yield return Fixture(pack, rule, EventDetectionFixtureKind.KnownFalsePositive, CreateMatchingObservations(rule),
                new[] { rule.RuleId }, rule.FalsePositives.FirstOrDefault() ??
                "An approved administrative action can be benign even though the rule correctly matches it.");
        }
    }

    private static EventDetectionFixture Fixture(
        EventDetectionPack pack,
        EventDetectionRuleDefinition rule,
        EventDetectionFixtureKind kind,
        IReadOnlyList<EventObservation> observations,
        IReadOnlyList<string> expected,
        string description) => new() {
            Name = $"{rule.RuleId} {kind}",
            PackId = pack.PackId,
            RuleId = rule.RuleId,
            Kind = kind,
            Description = description,
            Observations = observations,
            ExpectedRuleIds = expected
        };

    private static IReadOnlyList<EventObservation> CreateMatchingObservations(
        EventDetectionRuleDefinition rule) {

        DateTime start = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
        if (rule.Kind == EventDetectionRuleKind.Stateless) {
            return new[] { CreateObservation(rule, null, start, 1) };
        }
        if (rule.Kind is EventDetectionRuleKind.Threshold or EventDetectionRuleKind.DistinctValue) {
            return Enumerable.Range(0, rule.Threshold)
                .Select(index => CreateObservation(rule, null, start.AddTicks(index), index + 1, index))
                .ToArray();
        }
        return rule.Steps.Select((step, index) =>
            CreateObservation(rule, step, start.AddTicks(index), index + 1, index)).ToArray();
    }

    private static IReadOnlyList<EventObservation> CreateNegativeObservations(
        EventDetectionRuleDefinition rule) {

        DateTime start = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
        if (rule.Kind == EventDetectionRuleKind.Stateless) {
            return new[] { CreateObservation(rule, null, start, 1, typeNameOverride: "EVXFixtureUnrelated") };
        }
        if (rule.Kind is EventDetectionRuleKind.Threshold or EventDetectionRuleKind.DistinctValue) {
            return Enumerable.Range(0, Math.Max(1, rule.Threshold - 1))
                .Select(index => CreateObservation(rule, null, start.AddTicks(index), index + 1, index))
                .ToArray();
        }
        return rule.Steps.Take(rule.Steps.Count - 1)
            .Select((step, index) => CreateObservation(rule, step, start.AddTicks(index), index + 1, index))
            .ToArray();
    }

    private static IReadOnlyList<EventObservation> CreateBoundaryObservations(
        EventDetectionRuleDefinition rule) {

        DateTime start = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
        if (rule.Kind == EventDetectionRuleKind.Stateless) {
            return new[] { CreateObservation(rule, null, start, 1) };
        }
        if (rule.Kind is EventDetectionRuleKind.Threshold or EventDetectionRuleKind.DistinctValue) {
            return Enumerable.Range(0, rule.Threshold)
                .Select(index => CreateObservation(
                    rule,
                    null,
                    index == rule.Threshold - 1 ? start.Add(rule.Window) : start,
                    index + 1,
                    index))
                .ToArray();
        }
        return rule.Steps.Select((step, index) => CreateObservation(
            rule,
            step,
            index == rule.Steps.Count - 1 ? start.Add(rule.Window) : start,
            index + 1,
            index)).ToArray();
    }

    private static EventObservation CreateObservation(
        EventDetectionRuleDefinition rule,
        EventDetectionStepDefinition? step,
        DateTime time,
        long recordId,
        int distinctIndex = 0,
        string? typeNameOverride = null) {

        EventType? eventType = (step?.EventTypes ?? Array.Empty<EventType>()).Cast<EventType?>().FirstOrDefault() ??
                               rule.EventTypes.Cast<EventType?>().FirstOrDefault();
        string typeName = typeNameOverride ?? eventType?.ToString() ?? "Generic";
        EventSourceDefinition? sourceDefinition = eventType.HasValue
            ? EventTypeCatalog.GetSources(new[] { eventType.Value }).FirstOrDefault()
            : null;
        int eventId = (step?.EventIds ?? Array.Empty<int>()).FirstOrDefault();
        if (eventId == 0) {
            eventId = rule.EventIds.FirstOrDefault();
        }
        if (eventId == 0) {
            eventId = sourceDefinition?.EventIds.FirstOrDefault() ?? 1;
        }
        string channel = (step?.Channels ?? Array.Empty<string>()).FirstOrDefault() ??
                         rule.Channels.FirstOrDefault() ?? sourceDefinition?.LogName ?? "Security";
        string provider = (step?.Providers ?? Array.Empty<string>()).FirstOrDefault() ??
                          rule.Providers.FirstOrDefault() ?? "EventViewerX-Fixture";
        var metadata = new NativeEventMetadata(
            provider,
            providerId: null,
            eventId,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            time,
            recordId,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 2,
            channel,
            machineName: "fixture-host",
            userId: null,
            version: 1);
        var source = new EventObject(metadata, queriedMachine: "fixture-collector", containerLog: channel);
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["ObjectAffected"] = "EVOTEC\\fixture-user",
            ["Who"] = "EVOTEC\\fixture-admin",
            ["GroupName"] = "Domain Admins",
            ["SidHistory"] = "S-1-5-21-1-2-3-1000",
            ["WeakEncryptionAlgorithm"] = true,
            ["Account"] = "EVOTEC\\fixture-user",
            ["FixtureDistinctValue"] = "value-" + distinctIndex
        };
        if (!string.IsNullOrWhiteSpace(rule.GroupBy)) {
            fields[rule.GroupBy!] = "EVOTEC\\fixture-user";
        }
        if (!string.IsNullOrWhiteSpace(rule.DistinctBy)) {
            fields[rule.DistinctBy!] = "value-" + distinctIndex;
        }
        string identity = $"fixture:{rule.RuleId}:{recordId}:{time.Ticks}";
        return EventObservation.Restore(source, identity, typeName, fields, time, time);
    }
}
