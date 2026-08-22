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
    public void CompositeRequirementMergesAuditOutcomesForSharedPrerequisites() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.ActiveDirectoryChanges);
        EventPrerequisite accountManagement = Assert.Single(
            requirement.Prerequisites,
            static item => item.Key == "audit:user-account-management");

        Assert.Equal(
            EventAuditOutcome.Success | EventAuditOutcome.Failure,
            accountManagement.AuditOutcomes);
    }

    [Fact]
    public void GpoRequirementsIncludeAuditPolicyAndObjectSacl() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.GpoDeleted);

        Assert.Contains(requirement.Prerequisites, static item => item.Key == "audit:directory-service-changes");
        Assert.Contains(requirement.Prerequisites, static item => item.Key == "configuration:directory-object-sacl");
    }

    [Fact]
    public void GroupEnumerationDeclaresUserAccountManagementAuditing() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(
            EventType.ADGroupEnumeration);

        EventPrerequisite audit = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.AuditPolicy);
        Assert.Equal("audit:user-account-management", audit.Key);
        Assert.Equal(EventAuditOutcome.Success, audit.AuditOutcomes);
        Assert.Equal("Target computer", audit.AppliesTo);
        Assert.DoesNotContain(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.TargetRole);
    }

    [Fact]
    public void CertificateIssuanceDeclaresAuthorityAuditAndFilterRequirements() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(
            EventType.CertificateIssued);

        EventPrerequisite role = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.TargetRole);
        Assert.Equal("target-role:certification-authority", role.Key);
        EventPrerequisite audit = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.AuditPolicy);
        Assert.Equal("audit:certification-services", audit.Key);
        Assert.Equal(EventAuditOutcome.Success, audit.AuditOutcomes);
        Assert.Equal(new Guid("0CCE9221-69AE-11D9-BED3-505054503030"), audit.AuditSubcategoryGuid);
        Assert.Contains(
            requirement.Prerequisites,
            static item => item.Key == "configuration:certification-authority-audit-filter-requests");
    }

    [Theory]
    [InlineData(EventType.ADGroupMembershipChange)]
    [InlineData(EventType.ADGroupChange)]
    [InlineData(EventType.ADGroupCreateDelete)]
    public void GroupManagementCoversSecurityAndDistributionGroups(EventType type) {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);

        Assert.Contains(requirement.Prerequisites, static item =>
            item.Key == "audit:security-group-management" &&
            item.AuditOutcomes == EventAuditOutcome.Success);
        Assert.Contains(requirement.Prerequisites, static item =>
            item.Key == "audit:distribution-group-management" &&
            item.AuditOutcomes == EventAuditOutcome.Success);
    }

    [Fact]
    public void UserStatusCoversFailedPasswordChangeAttempts() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.ADUserStatus);
        EventPrerequisite audit = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.AuditPolicy);

        Assert.Equal(EventAuditOutcome.Success | EventAuditOutcome.Failure, audit.AuditOutcomes);
    }

    [Fact]
    public void NetworkPolicyEventsDeclareRoleAndBothAuditOutcomes() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(
            EventType.NetworkAccessAuthenticationPolicy);

        Assert.Contains(requirement.Prerequisites, static item =>
            item.Key == "target-role:network-policy-server");
        Assert.Contains(requirement.Prerequisites, static item =>
            item.Key == "audit:network-policy-server" &&
            item.AuditOutcomes == (EventAuditOutcome.Success | EventAuditOutcome.Failure));
    }

    [Theory]
    [InlineData(EventType.DeviceRecognized)]
    [InlineData(EventType.DeviceDisabled)]
    public void DeviceEventsDeclarePnpActivityAuditing(EventType type) {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);

        Assert.Contains(requirement.Prerequisites, static item =>
            item.Key == "audit:pnp-activity" &&
            item.AuditOutcomes == EventAuditOutcome.Success);
    }

    [Fact]
    public void Smb1AndObjectDeletionDeclareConfigurationEvidence() {
        EventTypeRequirement smb = EventRequirementCatalog.GetRequirement(EventType.ADSMBServerAuditV1);
        Assert.Contains(smb.Prerequisites, static item =>
            item.Key == "configuration:smb1-access-auditing");

        EventTypeRequirement deletion = EventRequirementCatalog.GetRequirement(EventType.ObjectDeletion);
        Assert.Contains(deletion.Prerequisites, static item =>
            item.Key == "configuration:object-deletion-audit-subcategory");
        Assert.Contains(deletion.Prerequisites, static item =>
            item.Key == "configuration:object-deletion-sacl");
    }

    [Theory]
    [InlineData(EventType.OSStartupSecurity)]
    [InlineData(EventType.OSCrashOnAuditFailRecovery)]
    public void SecurityLifecycleEventsDeclareSecurityStateChangeAuditing(EventType type) {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);

        Assert.Contains(requirement.Prerequisites, static item =>
            item.Key == "audit:security-state-change" &&
            item.AuditOutcomes == EventAuditOutcome.Success);
    }

    [Fact]
    public void BitLockerKeyEventsCoverPrivilegeAndDpapiAuditSources() {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(
            EventType.BitLockerKeyChange);
        string[] keys = requirement.Prerequisites
            .Where(static item => item.Kind == EventRequirementKind.AuditPolicy)
            .Select(static item => item.Key)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] {
                "audit:dpapi-activity",
                "audit:non-sensitive-privilege-use",
                "audit:sensitive-privilege-use"
            },
            keys);
        Assert.All(
            requirement.Prerequisites.Where(static item => item.Kind == EventRequirementKind.AuditPolicy),
            static item => Assert.Equal(
                EventAuditOutcome.Success | EventAuditOutcome.Failure,
                item.AuditOutcomes));
    }

    [Theory]
    [InlineData(EventType.LogsClearedSecurity)]
    [InlineData(EventType.LogsFullSecurity)]
    [InlineData(EventType.OSTimeChange)]
    public void PolicyIndependentSecurityEventsNeedOnlyTheirChannel(EventType type) {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);

        Assert.All(
            requirement.Prerequisites,
            static item => Assert.Equal(EventRequirementKind.EventChannel, item.Kind));
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

    [Theory]
    [InlineData(EventType.KerberosServiceTicket)]
    [InlineData(EventType.ADLdapBindingSummary)]
    public void DomainControllerEventsDeclareTheirRequiredSourceRole(EventType type) {
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);

        EventPrerequisite role = Assert.Single(
            requirement.Prerequisites,
            static item => item.Kind == EventRequirementKind.TargetRole);
        Assert.Equal("target-role:domain-controller", role.Key);
    }

    [Theory]
    [InlineData(EventType.ADUserPrivilegeUse, "audit:special-logon")]
    [InlineData(EventType.ADUserRightsAssignment, "audit:authorization-policy-change")]
    [InlineData(EventType.KerberosPolicyChange, "audit:authentication-policy-change")]
    public void DailyScenarioFamiliesDeclareTheirEffectiveAuditPolicy(
        EventType type,
        string requirementKey) {

        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);

        EventPrerequisite audit = Assert.Single(
            requirement.Prerequisites,
            item => string.Equals(item.Key, requirementKey, StringComparison.Ordinal));
        Assert.NotNull(audit.AuditSubcategoryGuid);
        Assert.NotEqual(EventAuditOutcome.None, audit.AuditOutcomes);
    }
}
