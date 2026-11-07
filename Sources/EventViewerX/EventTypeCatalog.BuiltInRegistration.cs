namespace EventViewerX;

/// <summary>Built-in projector registration that does not require runtime type discovery.</summary>
public static partial class EventTypeCatalog {
    private static bool _builtInRulesRegistered;

    /// <summary>
    /// Registers every built-in typed event projector through strongly referenced delegates.
    /// Call this before <see cref="Configure(EventRuleDiscoveryMode)"/> and select
    /// <see cref="EventRuleDiscoveryMode.ExplicitOnly"/> in trimmed or NativeAOT hosts.
    /// </summary>
    public static void RegisterBuiltInRules() {
        lock (_initLock) {
            if (_initialized) {
                throw new InvalidOperationException(
                    "Built-in rules must be registered before the first event-type query.");
            }
            if (_builtInRulesRegistered) {
                return;
            }

            RegisterBuiltInRuleFactories();
            _builtInRulesRegistered = true;
        }
    }

    private static void RegisterBuiltInRuleFactories(bool reflection = false) {
        RegisterBuiltInFactory<global::EventViewerX.Rules.AADConnect.AADConnectRunProfile>(reflection, EventType.AADConnectRunProfile, "Application", new[] { 906 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AADConnect.AADConnectRunProfile(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADComputerChangeDetailed>(reflection, EventType.ADComputerChangeDetailed, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADComputerChangeDetailed(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADComputerCreateChange>(reflection, EventType.ADComputerCreateChange, "Security", new[] { 4741, 4742 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADComputerCreateChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADComputerDeleted>(reflection, EventType.ADComputerDeleted, "Security", new[] { 4743 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADComputerDeleted(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupChange>(reflection, EventType.ADGroupChange, "Security", new[] { 4735, 4737, 4745, 4750, 4760, 4764, 4784, 4791 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupChangeDetailed>(reflection, EventType.ADGroupChangeDetailed, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupChangeDetailed(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupCreateDelete>(reflection, EventType.ADGroupCreateDelete, "Security", new[] { 4727, 4730, 4731, 4734, 4744, 4748, 4749, 4753, 4754, 4758, 4759, 4763 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupCreateDelete(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupEnumeration>(reflection, EventType.ADGroupEnumeration, "Security", new[] { 4798, 4799 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupEnumeration(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupMembershipChange>(reflection, EventType.ADGroupMembershipChange, "Security", new[] { 4728, 4729, 4732, 4733, 4746, 4747, 4751, 4752, 4756, 4757, 4761, 4762, 4785, 4786, 4787, 4788 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupMembershipChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyChanges>(reflection, EventType.ADGroupPolicyChanges, "Security", new[] { 5136, 5137, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyChanges(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyChangesDetailed>(reflection, EventType.ADGroupPolicyChangesDetailed, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyChangesDetailed(source), matchPriority: 100);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyEdits>(reflection, EventType.ADGroupPolicyEdits, "Security", new[] { 5136, 5137, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyEdits(source), matchPriority: 300);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyLinks>(reflection, EventType.ADGroupPolicyLinks, "Security", new[] { 5136, 5137, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADGroupPolicyLinks(source), matchPriority: 300);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADLdapBindingDetails>(reflection, EventType.ADLdapBindingDetails, "Directory Service", new[] { 2889 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADLdapBindingDetails(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADLdapBindingSummary>(reflection, EventType.ADLdapBindingSummary, "Directory Service", new[] { 2887 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADLdapBindingSummary(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADOrganizationalUnitChangeDetailed>(reflection, EventType.ADOrganizationalUnitChangeDetailed, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADOrganizationalUnitChangeDetailed(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADOtherChangeDetailed>(reflection, EventType.ADOtherChangeDetailed, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADOtherChangeDetailed(source), matchPriority: -100);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserChangeDetailed>(reflection, EventType.ADUserChangeDetailed, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserChangeDetailed(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserCreateChange>(reflection, EventType.ADUserCreateChange, "Security", new[] { 4720, 4738 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserCreateChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserLockouts>(reflection, EventType.ADUserLockouts, "Security", new[] { 4740 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserLockouts(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserLogon>(reflection, EventType.ADUserLogon, "Security", new[] { 4624 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserLogon(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserLogonFailed>(reflection, EventType.ADUserLogonFailed, "Security", new[] { 4625 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserLogonFailed(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserLogonNTLMv1>(reflection, EventType.ADUserLogonNTLMv1, "Security", new[] { 4624 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserLogonNTLMv1(source), matchPriority: 200);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserPrivilegeUse>(reflection, EventType.ADUserPrivilegeUse, "Security", new[] { 4672 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserPrivilegeUse(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserRightsAssignment>(reflection, EventType.ADUserRightsAssignment, "Security", new[] { 4704, 4705 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserRightsAssignment(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserStatus>(reflection, EventType.ADUserStatus, "Security", new[] { 4722, 4725, 4723, 4724, 4726 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserStatus(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.ADUserUnlocked>(reflection, EventType.ADUserUnlocked, "Security", new[] { 4767 }, static source => new global::EventViewerX.Rules.ActiveDirectory.ADUserUnlocked(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.GpoCreated>(reflection, EventType.GpoCreated, "Security", new[] { 5137 }, static source => new global::EventViewerX.Rules.ActiveDirectory.GpoCreated(source), matchPriority: 250);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.GpoDeleted>(reflection, EventType.GpoDeleted, "Security", new[] { 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.GpoDeleted(source), matchPriority: 250);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.GpoModified>(reflection, EventType.GpoModified, "Security", new[] { 5136 }, static source => new global::EventViewerX.Rules.ActiveDirectory.GpoModified(source), matchPriority: 250);
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.GroupPolicyDirectoryAudit>(reflection, EventType.GroupPolicyDirectoryAudit, "Security", new[] { 5136, 5137, 5139, 5141 }, static source => new global::EventViewerX.Rules.ActiveDirectory.GroupPolicyDirectoryAudit(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.PasswordSyncFailed>(reflection, EventType.AADConnectPasswordSyncFailed, "Application", new[] { 611 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.ActiveDirectory.PasswordSyncFailed(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.ActiveDirectory.SMBServerAudit>(reflection, EventType.ADSMBServerAuditV1, "Microsoft-Windows-SMBServer/Audit", new[] { 3000 }, static source => new global::EventViewerX.Rules.ActiveDirectory.SMBServerAudit(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADConnectStagingDisabled>(reflection, EventType.AADConnectStagingDisabled, "Application", new[] { 351 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AzureAD.AADConnectStagingDisabled(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADConnectStagingEnabled>(reflection, EventType.AADConnectStagingEnabled, "Application", new[] { 350 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AzureAD.AADConnectStagingEnabled(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADSyncCycleStage>(reflection, EventType.AADSyncCycleStage, "Application", new[] { 904 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AzureAD.AADSyncCycleStage(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADSyncFilterStatus>(reflection, EventType.AADSyncFilterStatus, "Application", new[] { 6952 }, new[] { "ADSync" }, static source => new global::EventViewerX.Rules.AzureAD.AADSyncFilterStatus(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADSyncImportStatus>(reflection, EventType.AADSyncImportStatus, "Application", new[] { 105, 132, 133, 134 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AzureAD.AADSyncImportStatus(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADSyncPasswordHashSyncStatus>(reflection, EventType.AADSyncPasswordHashSyncStatus, "Application", new[] { 663 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AzureAD.AADSyncPasswordHashSyncStatus(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.AzureAD.AADSyncProvisionCredentialsPing>(reflection, EventType.AADSyncProvisionCredentialsPing, "Application", new[] { 653, 654 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.AzureAD.AADSyncProvisionCredentialsPing(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.CertificateAuthority.CertificateIssued>(reflection, EventType.CertificateIssued, "Security", new[] { 4886, 4887 }, static source => new global::EventViewerX.Rules.CertificateAuthority.CertificateIssued(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.DHCP.DhcpLeaseCreated>(reflection, EventType.DhcpLeaseCreated, "Microsoft-Windows-DHCP Server/Operational", new[] { 10 }, static source => new global::EventViewerX.Rules.DHCP.DhcpLeaseCreated(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Exchange.ExchangeDatabaseMounted>(reflection, EventType.ExchangeDatabaseMounted, "Application", new[] { 9526 }, static source => new global::EventViewerX.Rules.Exchange.ExchangeDatabaseMounted(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.HyperV.HyperVVirtualMachineStarted>(reflection, EventType.HyperVVirtualMachineStarted, "Microsoft-Windows-Hyper-V-VMMS/Admin", new[] { 18500 }, static source => new global::EventViewerX.Rules.HyperV.HyperVVirtualMachineStarted(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.HyperV.VmCheckpointCreated>(reflection, EventType.HyperVCheckpointCreated, "Microsoft-Windows-Hyper-V-VMMS/Admin", new[] { 4096 }, static source => new global::EventViewerX.Rules.HyperV.VmCheckpointCreated(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.HyperV.VmShutdown>(reflection, EventType.HyperVVirtualMachineShutdown, "Microsoft-Windows-Hyper-V-Worker/Operational", new[] { 18560 }, static source => new global::EventViewerX.Rules.HyperV.VmShutdown(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.IIS.IISSiteBindingFailure>(reflection, EventType.IISSiteBindingFailure, "System", new[] { 1007 }, static source => new global::EventViewerX.Rules.IIS.IISSiteBindingFailure(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.IIS.IISSiteStopped>(reflection, EventType.IISSiteStopped, "System", new[] { 1005 }, static source => new global::EventViewerX.Rules.IIS.IISSiteStopped(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Kerberos.KerberosPolicyChange>(reflection, EventType.KerberosPolicyChange, "Security", new[] { 4713 }, static source => new global::EventViewerX.Rules.Kerberos.KerberosPolicyChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Kerberos.KerberosKdcRc4Audit>(reflection, EventType.KerberosKdcRc4Audit, "System", new[] { 201, 202, 203, 204, 205, 206, 207, 208, 209 }, new[] { "Kdcsvc" }, static source => new global::EventViewerX.Rules.Kerberos.KerberosKdcRc4Audit(source), canHandle: static source => global::EventViewerX.Rules.RuleHelpers.IsProvider(source, "Kdcsvc"));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Kerberos.KerberosServiceTicket>(reflection, EventType.KerberosServiceTicket, "Security", new[] { 4769, 4770 }, static source => new global::EventViewerX.Rules.Kerberos.KerberosServiceTicket(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Kerberos.KerberosTGTRequest>(reflection, EventType.KerberosTGTRequest, "Security", new[] { 4768 }, static source => new global::EventViewerX.Rules.Kerberos.KerberosTGTRequest(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Kerberos.KerberosTicketFailure>(reflection, EventType.KerberosTicketFailure, "Security", new[] { 4771, 4772 }, static source => new global::EventViewerX.Rules.Kerberos.KerberosTicketFailure(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Logging.LogsClearedOther>(reflection, EventType.LogsClearedOther, "System", new[] { 104 }, static source => new global::EventViewerX.Rules.Logging.LogsClearedOther(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Logging.LogsClearedSecurity>(reflection, EventType.LogsClearedSecurity, "Security", new[] { 1102, 1105 }, static source => new global::EventViewerX.Rules.Logging.LogsClearedSecurity(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Logging.LogsFullSecurity>(reflection, EventType.LogsFullSecurity, "Security", new[] { 1104 }, static source => new global::EventViewerX.Rules.Logging.LogsFullSecurity(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.NPS.NetworkAccessAuthenticationPolicy>(reflection, EventType.NetworkAccessAuthenticationPolicy, "Security", new[] { 6272, 6273 }, static source => new global::EventViewerX.Rules.NPS.NetworkAccessAuthenticationPolicy(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.SQL.SqlDatabaseCreated>(reflection, EventType.SqlDatabaseCreated, "Application", new[] { 17137, 1802 }, static source => new global::EventViewerX.Rules.SQL.SqlDatabaseCreated(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.AuditPolicyChange>(reflection, EventType.AuditPolicyChange, "Security", new[] { 4719 }, static source => new global::EventViewerX.Rules.Windows.AuditPolicyChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.BitLockerKeyChange>(reflection, EventType.BitLockerKeyChange, "Security", new[] { 4673, 4692 }, static source => new global::EventViewerX.Rules.Windows.BitLockerKeyChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.BitLockerSuspended>(reflection, EventType.BitLockerSuspended, "BitLocker-API", new[] { 24660 }, static source => new global::EventViewerX.Rules.Windows.BitLockerSuspended(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ClientGroupPoliciesApplication>(reflection, EventType.ClientGroupPoliciesApplication, "Application", new[] { 4098 }, static source => new global::EventViewerX.Rules.Windows.ClientGroupPoliciesApplication(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ClientGroupPoliciesSystem>(reflection, EventType.ClientGroupPoliciesSystem, "System", new[] { 1085 }, static source => new global::EventViewerX.Rules.Windows.ClientGroupPoliciesSystem(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DefenderConfigurationChanged>(reflection, EventType.DefenderConfigurationChanged, "Microsoft-Windows-Windows Defender/Operational", new[] { 5007 }, static source => new global::EventViewerX.Rules.Windows.DefenderConfigurationChanged(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DefenderThreatAction>(reflection, EventType.DefenderThreatAction, "Microsoft-Windows-Windows Defender/Operational", new[] { 1117 }, static source => new global::EventViewerX.Rules.Windows.DefenderThreatAction(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DefenderThreatDetected>(reflection, EventType.DefenderThreatDetected, "Microsoft-Windows-Windows Defender/Operational", new[] { 1116 }, static source => new global::EventViewerX.Rules.Windows.DefenderThreatDetected(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DeviceDisabled>(reflection, EventType.DeviceDisabled, "Security", new[] { 6420 }, static source => new global::EventViewerX.Rules.Windows.DeviceDisabled(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DeviceRecognized>(reflection, EventType.DeviceRecognized, "Security", new[] { 6416 }, static source => new global::EventViewerX.Rules.Windows.DeviceRecognized(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DfsReplicationError>(reflection, EventType.DfsReplicationError, "DFS Replication", new[] { 5002 }, static source => new global::EventViewerX.Rules.Windows.DfsReplicationError(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.FirewallRuleAdded>(reflection, EventType.FirewallRuleAdded, "Security", new[] { 4946 }, static source => new global::EventViewerX.Rules.Windows.FirewallRuleAdded(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.FirewallRuleChange>(reflection, EventType.FirewallRuleChange, "Security", new[] { 4947 }, static source => new global::EventViewerX.Rules.Windows.FirewallRuleChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.FirewallRuleDeleted>(reflection, EventType.FirewallRuleDeleted, "Security", new[] { 4948 }, static source => new global::EventViewerX.Rules.Windows.FirewallRuleDeleted(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.NetworkMonitorDriverLoaded>(reflection, EventType.NetworkMonitorDriverLoaded, "System", new[] { 6, 7035, 7045 }, static source => new global::EventViewerX.Rules.Windows.NetworkMonitorDriverLoaded(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.NetworkPromiscuousMode>(reflection, EventType.NetworkPromiscuousMode, "System", new[] { 10400, 10401 }, static source => new global::EventViewerX.Rules.Windows.NetworkPromiscuousMode(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ObjectDeletion>(reflection, EventType.ObjectDeletion, "Security", new[] { 4660 }, static source => new global::EventViewerX.Rules.Windows.ObjectDeletion(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSBugCheck>(reflection, EventType.OSBugCheck, "System", new[] { 1001 }, static source => new global::EventViewerX.Rules.Windows.OSBugCheck(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSCrash>(reflection, EventType.OSCrash, "System", new[] { 6008 }, static source => new global::EventViewerX.Rules.Windows.OSCrash(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSCrashOnAuditFailRecovery>(reflection, EventType.OSCrashOnAuditFailRecovery, "Security", new[] { 4621 }, static source => new global::EventViewerX.Rules.Windows.OSCrashOnAuditFailRecovery(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSShutdown>(reflection, EventType.OSShutdown, "System", new[] { 13 }, static source => new global::EventViewerX.Rules.Windows.OSShutdown(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSStartup>(reflection, EventType.OSStartup, "System", new[] { 12 }, static source => new global::EventViewerX.Rules.Windows.OSStartup(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSStartupSecurity>(reflection, EventType.OSStartupSecurity, "Security", new[] { 4608 }, static source => new global::EventViewerX.Rules.Windows.OSStartupSecurity(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSTimeChange>(reflection, EventType.OSTimeChange, "Security", new[] { 4616 }, static source => new global::EventViewerX.Rules.Windows.OSTimeChange(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.OSUncleanShutdown>(reflection, EventType.OSUncleanShutdown, "System", new[] { 41 }, static source => new global::EventViewerX.Rules.Windows.OSUncleanShutdown(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ScheduledTaskCreated>(reflection, EventType.ScheduledTaskCreated, "Security", new[] { 4698 }, static source => new global::EventViewerX.Rules.Windows.ScheduledTaskCreated(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ScheduledTaskDeleted>(reflection, EventType.ScheduledTaskDeleted, "Security", new[] { 4699 }, static source => new global::EventViewerX.Rules.Windows.ScheduledTaskDeleted(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ScheduledTaskDisabled>(reflection, EventType.ScheduledTaskDisabled, "Security", new[] { 4701 }, static source => new global::EventViewerX.Rules.Windows.ScheduledTaskDisabled(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ScheduledTaskEnabled>(reflection, EventType.ScheduledTaskEnabled, "Security", new[] { 4700 }, static source => new global::EventViewerX.Rules.Windows.ScheduledTaskEnabled(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ScheduledTaskUpdated>(reflection, EventType.ScheduledTaskUpdated, "Security", new[] { 4702 }, static source => new global::EventViewerX.Rules.Windows.ScheduledTaskUpdated(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.SyncCompleted>(reflection, EventType.SyncCompleted, "Application", new[] { 907 }, new[] { "Directory Synchronization" }, static source => new global::EventViewerX.Rules.Windows.SyncCompleted(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.WindowsUpdateFailure>(reflection, EventType.WindowsUpdateFailure, "Setup", new[] { 20 }, static source => new global::EventViewerX.Rules.Windows.WindowsUpdateFailure(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ApplicationCrash>(reflection, EventType.ApplicationCrash, "Application", new[] { 1000 }, new[] { "Application Error" }, static source => new global::EventViewerX.Rules.Windows.ApplicationCrash(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ApplicationHang>(reflection, EventType.ApplicationHang, "Application", new[] { 1002 }, new[] { "Application Hang" }, static source => new global::EventViewerX.Rules.Windows.ApplicationHang(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ServiceFailure>(reflection, EventType.ServiceFailure, "System", new[] { 7000, 7009, 7023, 7031, 7034 }, new[] { "Service Control Manager" }, static source => new global::EventViewerX.Rules.Windows.ServiceFailure(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DiskError>(reflection, EventType.DiskError, "System", new[] { 7, 51, 153 }, new[] { "disk" }, static source => new global::EventViewerX.Rules.Windows.DiskError(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.FileSystemError>(reflection, EventType.FileSystemError, "System", new[] { 55, 98 }, new[] { "Ntfs", "Microsoft-Windows-Ntfs" }, static source => new global::EventViewerX.Rules.Windows.FileSystemError(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.StorageReset>(reflection, EventType.StorageReset, "System", new[] { 129 }, new[] { "stornvme" }, static source => new global::EventViewerX.Rules.Windows.StorageReset(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.HardwareError>(reflection, EventType.HardwareError, "System", new[] { 17, 18, 19, 47 }, new[] { "Microsoft-Windows-WHEA-Logger" }, static source => new global::EventViewerX.Rules.Windows.HardwareError(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.DisplayDriverReset>(reflection, EventType.DisplayDriverReset, "System", new[] { 4101 }, new[] { "Display" }, static source => new global::EventViewerX.Rules.Windows.DisplayDriverReset(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.BootPerformance>(reflection, EventType.BootPerformance, "Microsoft-Windows-Diagnostics-Performance/Operational", new[] { 100 }, new[] { "Microsoft-Windows-Diagnostics-Performance" }, static source => new global::EventViewerX.Rules.Windows.BootPerformance(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ShutdownPerformance>(reflection, EventType.ShutdownPerformance, "Microsoft-Windows-Diagnostics-Performance/Operational", new[] { 200 }, new[] { "Microsoft-Windows-Diagnostics-Performance" }, static source => new global::EventViewerX.Rules.Windows.ShutdownPerformance(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.StartupDegradation>(reflection, EventType.StartupDegradation, "Microsoft-Windows-Diagnostics-Performance/Operational", new[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 }, new[] { "Microsoft-Windows-Diagnostics-Performance" }, static source => new global::EventViewerX.Rules.Windows.StartupDegradation(source));
        RegisterBuiltInFactory<global::EventViewerX.Rules.Windows.ShutdownDegradation>(reflection, EventType.ShutdownDegradation, "Microsoft-Windows-Diagnostics-Performance/Operational", new[] { 203 }, new[] { "Microsoft-Windows-Diagnostics-Performance" }, static source => new global::EventViewerX.Rules.Windows.ShutdownDegradation(source));
    }

    private static void RegisterBuiltInFactory<
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        TRecord>(bool reflection, EventType type, string logName, IReadOnlyList<int> eventIds,
        Func<EventObject, EventTypeRecord> factory, int matchPriority = 0, Func<EventObject, bool>? canHandle = null) =>
        RegisterBuiltInFactory<TRecord>(reflection, type, logName, eventIds,
            Array.Empty<string>(), factory, matchPriority, canHandle);

    private static void RegisterBuiltInFactory<
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        TRecord>(bool reflection, EventType type, string logName, IReadOnlyList<int> eventIds,
        IReadOnlyList<string> providers, Func<EventObject, EventTypeRecord> factory, int matchPriority = 0, Func<EventObject, bool>? canHandle = null) {
        if (!reflection) {
            RegisterRuleFactoryCore(type, logName, eventIds, providers, factory,
                canHandle, typeof(TRecord), matchPriority);
            return;
        }
        var registration = new RuleFactoryRegistration(type, logName, eventIds, factory,
            canHandle, typeof(TRecord), matchPriority, providers);
        _reflectionRuleTypes[type] = registration;
        _reflectionRulePriorities[type] = matchPriority;
        _reflectionRuleSources[type] = (logName, eventIds, providers);
        _reflectionProjectors[type] = new EventProjectorDefinition(
            type, typeof(TRecord).FullName ?? typeof(TRecord).Name, factory, canHandle);
        foreach (int eventId in eventIds) {
            var key = (eventId, logName);
            if (!_reflectionHandlers.TryGetValue(key, out List<Type>? handlers)) {
                handlers = new List<Type>();
                _reflectionHandlers[key] = handlers;
            }
            handlers.Add(typeof(TRecord));
        }
    }
}
