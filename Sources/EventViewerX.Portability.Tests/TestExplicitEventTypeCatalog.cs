using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Security.Principal;
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
        var source = new EventObject(new SecurityEventRecord(4624), "DC01", EventReadMode.Metadata);
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

    private sealed class SecurityEventRecord : EventRecord {
        private readonly int _id;

        internal SecurityEventRecord(int id) {
            _id = id;
        }

        public override string ProviderName => "Microsoft-Windows-Security-Auditing";
        public override string LogName => "Security";
        public override string MachineName => "dc01.ad.evotec.xyz";
        public override int Id => _id;
        public override byte? Level => 0;
        public override int? Task => 12544;
        public override long? Keywords => 0;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 1;
        public override int? ThreadId => 1;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
        public override int? Qualifiers => null;
        public override long? RecordId => 42;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        public override string FormatDescription() => string.Empty;
        public override string FormatDescription(IEnumerable<object> values) => string.Empty;
        public override string ToXml() => string.Empty;
    }
}
