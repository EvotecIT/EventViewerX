using System.Runtime.CompilerServices;
using Xunit;

namespace EventViewerX.Portability.Tests;

internal static class ExplicitEventTypeCatalogBootstrap {
    [ModuleInitializer]
    internal static void Initialize() {
        EventTypeCatalog.RegisterBuiltInRules();
        EventTypeCatalog.RegisterBuiltInRules();
        EventTypeCatalog.Configure(EventRuleDiscoveryMode.ExplicitOnly);
    }
}

public sealed class TestExplicitEventTypeCatalog {
    [Fact]
    public void ExplicitCatalogContainsEveryBuiltInLeafAndSource() {
        EventTypeDefinition[] definitions = EventTypeCatalog.GetDefinitions().ToArray();
        EventTypeDefinition[] leaves = definitions.Where(static definition => !definition.IsComposite).ToArray();

        Assert.Equal(Enum.GetValues<EventType>().Length, definitions.Length);
        Assert.Equal(89, leaves.Length);
        Assert.All(leaves, static definition => {
            Assert.NotNull(definition.RecordType);
            Assert.NotEmpty(definition.Sources);
            Assert.NotEmpty(definition.Fields);
        });

        EventTypeProjectionPlan plan = EventTypeCatalog.CompileProjectionPlan(
            leaves.Select(static definition => definition.Type));
        foreach (EventTypeDefinition definition in leaves) {
            foreach (EventSourceDefinition source in definition.Sources) {
                foreach (int eventId in source.EventIds) {
                    Assert.Contains(
                        plan.GetCandidates(eventId, source.LogName),
                        candidate => candidate.Type == definition.Type);
                }
            }
        }
    }

    [Fact]
    public void ExplicitCatalogPreservesSpecificityAndTypedProjection() {
        EventObject source = new SavedEventRecord {
            ProviderName = "Microsoft-Windows-Security-Auditing",
            EventId = 4624,
            RecordId = 42,
            Channel = "Security",
            Computer = "dc01.ad.evotec.xyz",
            TimeCreatedUtc = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc)
        }.ToEventObject("portable-fixture.evtx", EventReadMode.Metadata);
        source.Data["LmPackageName"] = "NTLM V1";

        EventTypeRecord? projected = EventTypeCatalog.CreateEventRule(
            source,
            new[] { EventType.ActiveDirectoryAuthentication });

        Assert.IsType<Rules.ActiveDirectory.ADUserLogonNTLMv1>(projected);
    }

    [Fact]
    public void ExplicitCatalogRejectsLateRegistration() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            EventTypeCatalog.RegisterBuiltInRules);

        Assert.Contains("before the first event-type query", exception.Message, StringComparison.Ordinal);
    }

}
