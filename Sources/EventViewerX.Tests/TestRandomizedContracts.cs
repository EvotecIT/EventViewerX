using System.Security;
using EventViewerX.Native;
using EventViewerX.Rules.ActiveDirectory;
using EventViewerX.Sigma;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestRandomizedContracts {
    [Fact]
    public void SavedXmlPayloadProjectionRoundTripsDeterministicRandomValues() {
        var random = new Random(730_513);
        DateTime timestamp = new(2026, 8, 29, 8, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 128; index++) {
            string value = RandomText(random, random.Next(0, 96));
            string escaped = SecurityElement.Escape(value) ?? string.Empty;
            string xml =
                "<Event xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\">" +
                "<System><Provider Name=\"Randomized-Provider\"/><EventID>7001</EventID>" +
                $"<TimeCreated SystemTime=\"{timestamp:O}\"/><EventRecordID>{index + 1}</EventRecordID>" +
                "<Channel>Randomized/Operational</Channel><Computer>fixture-host</Computer></System>" +
                $"<EventData><Data Name=\"RandomValue\">{escaped}</Data></EventData></Event>";

            SavedEventRecord record = SavedEventXmlProjector.Create(xml);

            Assert.Equal(value, record.Data["RandomValue"]);
            Assert.Equal(index + 1, record.RecordId);
        }
    }

    [Fact]
    public void GroupPolicyLinkFlagsPreserveEveryLowByteCombination() {
        for (int options = 0; options <= byte.MaxValue; options++) {
            EventObject source = CreateEvent(5136, options + 1);
            source.Data["ObjectClass"] = "organizationalUnit";
            source.Data["AttributeLDAPDisplayName"] = "gPLink";
            source.Data["AttributeValue"] =
                $"[LDAP://cn={{11111111-2222-3333-4444-555555555555}},cn=policies,cn=system,DC=example,DC=test;{options}]";
            source.Data["OperationType"] = "%%14674";

            var typed = Assert.IsType<ADGroupPolicyLinks>(
                EventTypeCatalog.CreateEventRule(source, new[] { EventType.ADGroupPolicyLinks }));
            GroupPolicyLinks link = Assert.Single(typed.GroupPolicyLink);

            Assert.Equal(options, link.Options);
            Assert.Equal((options & 0x1) == 0, link.IsEnabled);
            Assert.Equal((options & 0x2) != 0, link.IsEnforced);
        }
    }

    [Fact]
    public void StoreSelectorsNormalizeRandomCaseAndWhitespaceWithoutAliasing() {
        var random = new Random(992_177);
        string[] canonical = Enumerable.Range(0, 64).Select(index => $"host-{index:D3}").ToArray();
        string[] inputs = canonical
            .SelectMany(value => new[] { " " + value + " ", RandomCase(random, value) })
            .OrderBy(_ => random.Next())
            .ToArray();

        EventStoreQuery query = new EventStoreQueryBuilder { SourceComputers = inputs }.Build();
        inputs[0] = "mutated";

        Assert.Equal(canonical.Length, query.SourceComputers!.Count);
        Assert.Empty(canonical.Except(query.SourceComputers, StringComparer.OrdinalIgnoreCase));
        Assert.All(query.SourceComputers, static value => Assert.Equal(value.Trim(), value));
    }

    [Fact]
    public void RandomSigmaEventIdConditionsRetainExactMatchSemantics() {
        var random = new Random(441_709);
        DateTime timestamp = new(2026, 8, 29, 8, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 64; index++) {
            int eventId = random.Next(1, 65_535);
            string yaml = $"""
                title: Randomized exact event identifier {index}
                id: 11111111-2222-4333-8444-{index:D12}
                logsource:
                  product: windows
                detection:
                  selection:
                    EventID: {eventId}
                  condition: selection
                """;
            EventDetectionPlan plan = SigmaRuleCompiler.CompileYaml(yaml).CompilePlan();
            EventObservation miss = Observe(CreateEvent(eventId == 1 ? 2 : eventId - 1, index * 2 + 1), timestamp);
            EventObservation match = Observe(CreateEvent(eventId, index * 2 + 2), timestamp.AddTicks(1));

            EventDetectionFinding finding = Assert.Single(
                EventDetectionEngine.Stream(new[] { miss, match }, plan));

            Assert.Equal(eventId, Assert.Single(finding.Evidence).EventId);
        }
    }

    [Fact]
    public void RandomCorrelationGroupsFailClosedAtDeclaredBound() {
        var rule = new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "EVX-RANDOM-BOUNDS",
            Title = "Randomized group bound",
            Kind = EventDetectionRuleKind.Threshold,
            EventIds = new[] { 7002 },
            Threshold = 2,
            Window = TimeSpan.FromMinutes(5),
            GroupBy = "Account"
        });
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { rule });
        DateTime timestamp = new(2026, 8, 29, 8, 0, 0, DateTimeKind.Utc);
        EventObservation[] observations = Enumerable.Range(0, 100)
            .Select(index => {
                EventObject source = CreateEvent(7002, index + 1);
                source.Data["Account"] = $"account-{index:D3}";
                return Observe(source, timestamp.AddTicks(index));
            })
            .ToArray();

        EventDetectionFinding incomplete = Assert.Single(EventDetectionEngine.Stream(
            observations,
            plan,
            new EventDetectionEngineOptions(maximumGroups: 8)));

        Assert.Equal(EventDetectionFindingStatus.Incomplete, incomplete.Status);
        Assert.Contains("MaximumGroups", incomplete.CompletenessDiagnostic, StringComparison.Ordinal);
    }

    private static EventObservation Observe(EventObject source, DateTime timestamp) =>
        EventObservation.Create(source, receivedTimeUtc: timestamp, processedTimeUtc: timestamp);

    private static EventObject CreateEvent(int eventId, long recordId) => new(
        new NativeEventMetadata(
            "Randomized-Provider",
            providerId: null,
            eventId,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            new DateTime(2026, 8, 29, 8, 0, 0, DateTimeKind.Utc).AddTicks(recordId),
            recordId,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 2,
            "Security",
            "fixture-host",
            userId: null,
            version: 1),
        "fixture-collector",
        "Security");

    private static string RandomText(Random random, int length) {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 <>&'\"_-";
        return new string(Enumerable.Range(0, length)
            .Select(_ => alphabet[random.Next(alphabet.Length)])
            .ToArray());
    }

    private static string RandomCase(Random random, string value) => new(value
        .Select(character => random.Next(2) == 0
            ? char.ToLowerInvariant(character)
            : char.ToUpperInvariant(character))
        .ToArray());
}
