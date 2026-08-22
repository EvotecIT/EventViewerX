using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventRequirementCatalog {
    [Fact]
    public void EveryTypeHasChannelRequirementsMatchingItsNativeSources() {
        foreach (EventType type in Enum.GetValues(typeof(EventType))) {
            EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);
            EventTypeDefinition definition = EventTypeCatalog.GetDefinition(type);

            Assert.Equal(definition.Sources.Select(static source => source.LogName),
                requirement.Sources.Select(static source => source.LogName));
            Assert.Equal(requirement.Prerequisites.Count,
                requirement.Prerequisites.Select(static item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (EventSourceDefinition source in definition.Sources) {
                Assert.Contains(requirement.Prerequisites, item =>
                    item.Kind == EventRequirementKind.EventChannel &&
                    string.Equals(item.Name, source.LogName, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void WeakAuthenticationRequirementsExposeAuditAndVolumeEvidence() {
        EventTypeRequirement ntlm = EventRequirementCatalog.GetRequirement(EventType.ADUserLogonNTLMv1);
        EventPrerequisite ntlmAudit = Assert.Single(ntlm.Prerequisites, static item => item.Kind == EventRequirementKind.AuditPolicy);
        Assert.Equal(EventAuditOutcome.Success, ntlmAudit.AuditOutcomes);
        Assert.Equal("Audit Logon", ntlmAudit.Name);

        EventTypeRequirement kerberos = EventRequirementCatalog.GetRequirement(EventType.KerberosServiceTicket);
        EventPrerequisite kerberosAudit = Assert.Single(kerberos.Prerequisites, static item => item.Kind == EventRequirementKind.AuditPolicy);
        Assert.Equal(EventAuditOutcome.Success | EventAuditOutcome.Failure, kerberosAudit.AuditOutcomes);
        Assert.Equal(EventRequirementVolume.VeryHigh, kerberosAudit.Volume);
        Assert.NotNull(kerberosAudit.DocumentationUri);
    }

    [Fact]
    public void CompositeRequirementUnionsLeafPrerequisites() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.ActiveDirectoryAuthentication);

        Assert.True(requirement.IsComposite);
        Assert.Contains(EventType.ADUserLogonNTLMv1, requirement.IncludedTypes);
        Assert.Contains(EventType.KerberosServiceTicket, requirement.IncludedTypes);
        Assert.Contains(requirement.Prerequisites, static item => item.Key == "audit:logon-success");
        Assert.Contains(requirement.Prerequisites, static item => item.Key == "audit:kerberos-service-ticket");
    }

    [Fact]
    public void GpoRequirementsIncludeAuditPolicyAndObjectSacl() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.GpoDeleted);

        Assert.Contains(requirement.Prerequisites, static item => item.Key == "audit:directory-service-changes");
        Assert.Contains(requirement.Prerequisites, static item => item.Key == "configuration:directory-object-sacl");
    }

    [Fact]
    public void LdapDetailsExplainDiagnosticConfigurationWithoutMutatingIt() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.ADLdapBindingDetails);
        EventPrerequisite configuration = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.Configuration);

        Assert.Contains("level 2", configuration.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not change", configuration.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DomainControllerEventsDeclareTheirRequiredSourceRole() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.KerberosServiceTicket);

        EventPrerequisite role = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.TargetRole);
        Assert.Equal("target-role:domain-controller", role.Key);
    }
}
