using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventDecisionReports {
    [Fact]
    public void CatalogDefinesEveryDecisionReportKind() {
        IReadOnlyList<EventDecisionReportDefinition> definitions =
            EventDecisionReportCatalog.GetDefinitions();

        Assert.Equal(Enum.GetValues(typeof(EventDecisionReportKind)).Length, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(static item => item.Kind).Distinct().Count());
        Assert.All(definitions, static definition => {
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        });
    }

    [Fact]
    public void ProfilesFilterOneEvaluationWithoutRerunningTheSourceQuery() {
        EventObject ntlm = CreateEvent(4624, "Security", 20, new Dictionary<string, string> {
            ["LmPackageName"] = "NTLM V1",
            ["TargetUserName"] = "alice",
            ["TargetDomainName"] = "CONTOSO"
        });
        EventObject generic = CreateEvent(9001, "Application", 21);
        EventDetectionPack[] packs = EventDetectionCatalog.GetBuiltInPacks().ToArray();
        EventDetectionPlan plan = EventDetectionPlan.Compile(
            packs.SelectMany(static pack => pack.GetRules()));
        EventDetectionExecutionResult execution = EventDetectionEngine.Evaluate(
            new[] { generic, ntlm },
            plan);

        EventDecisionReportSnapshot authentication = EventDecisionReportEngine.Create(
            EventDecisionReportKind.AuthenticationPosture,
            execution.Observations,
            execution.Findings,
            packs,
            new EventDetectionReportOptions {
                QueryOwner = "Decision report fixture",
                UsedStorageHistory = true,
                Limits = new[] { "Maximum observations: 100" }
            });
        EventDecisionReportSnapshot unknown = EventDecisionReportEngine.Create(
            EventDecisionReportKind.UnknownEventAndSchemaDrift,
            execution.Observations,
            execution.Findings,
            packs);
        EventDecisionReportSnapshot health = EventDecisionReportEngine.Create(
            EventDecisionReportKind.DetectionHealth,
            execution.Observations,
            execution.Findings,
            packs);

        Assert.Equal(2, execution.Observations.Count);
        Assert.Single(authentication.Analysis.Findings, static finding => finding.RuleId == "EVX-AUTH-0001");
        Assert.Contains(authentication.Analysis.Observations,
            static observation => observation.TypeName == nameof(EventType.ADUserLogonNTLMv1));
        Assert.Equal("Decision report fixture", authentication.Analysis.QueryOwner);
        Assert.True(authentication.Analysis.UsedStorageHistory);
        Assert.Single(authentication.Analysis.Limits);
        Assert.Single(unknown.Analysis.Observations,
            static observation => observation.TypeName == "Generic");
        Assert.Equal(packs.Length, health.Analysis.Packs.Count);
        Assert.Contains(authentication.Metrics,
            static metric => metric.Name == "NtlmV1Count" && metric.Value == 1);
        Assert.Contains(authentication.Analysis.PresentationReport.Sections,
            static section => section.Name == "DecisionMetric");
        Assert.NotEmpty(authentication.Analysis.PresentationReport.Sections);
    }

    private static EventObject CreateEvent(
        int eventId,
        string logName,
        long recordId,
        IReadOnlyDictionary<string, string>? data = null) {

        var metadata = new NativeEventMetadata(
            "Microsoft-Windows-Security-Auditing",
            Guid.Parse("54849625-5478-4994-A5BA-3E3B0328C30D"),
            eventId,
            qualifiers: null,
            level: 0,
            task: 12544,
            opcode: 0,
            keywords: 0,
            timeCreated: new DateTime(2026, 8, 29, 8, 0, 0, DateTimeKind.Utc).AddSeconds(recordId),
            recordId: recordId,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 1,
            logName: logName,
            machineName: "server01",
            userId: null,
            version: 1);
        var source = new EventObject(metadata, queriedMachine: "server01", containerLog: logName);
        foreach (KeyValuePair<string, string> item in data ?? new Dictionary<string, string>()) {
            source.Data[item.Key] = item.Value;
        }
        return source;
    }
}
