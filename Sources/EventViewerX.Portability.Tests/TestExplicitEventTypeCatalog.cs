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
        Assert.Equal(90, leaves.Length);
        Assert.Equal(102, (int)EventType.GroupPolicyDirectoryAudit);
        Assert.Equal(103, (int)EventType.KerberosKdcRc4Audit);
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
    public void ExplicitCatalogRestrictsKdcRc4IdsToTheKdcProvider() {
        EventObject kdc = CreateSavedSystemEvent("Kdcsvc");
        EventObject unrelated = CreateSavedSystemEvent("Unrelated-System-Provider");
        EventObject unverifiedAlias = CreateSavedSystemEvent("Microsoft-Windows-Kerberos-Key-Distribution-Center");

        Assert.IsType<Rules.Kerberos.KerberosKdcRc4Audit>(
            EventTypeCatalog.CreateEventRule(kdc, new[] { EventType.KerberosKdcRc4Audit }));
        Assert.Null(EventTypeCatalog.CreateEventRule(
            unrelated,
            new[] { EventType.KerberosKdcRc4Audit }));
        Assert.Null(EventTypeCatalog.CreateEventRule(
            unverifiedAlias,
            new[] { EventType.KerberosKdcRc4Audit }));
    }

    [Fact]
    public void ExplicitCatalogRejectsLateRegistration() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            EventTypeCatalog.RegisterBuiltInRules);

        Assert.Contains("before the first event-type query", exception.Message, StringComparison.Ordinal);
    }

    private static EventObject CreateSavedSystemEvent(string provider) => new SavedEventRecord {
        ProviderName = provider,
        EventId = 201,
        RecordId = 43,
        Channel = "System",
        Computer = "dc01.ad.evotec.xyz",
        TimeCreatedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
    }.ToEventObject("portable-fixture.evtx", EventReadMode.Metadata);

}
