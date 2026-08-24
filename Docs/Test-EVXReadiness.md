---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Test-EVXReadiness
## SYNOPSIS
Assesses EventViewerX prerequisites without changing Windows configuration.

Composes explicit target discovery, native Event Log probes, effective local audit policy, observed-event evidence, and safe provider configuration checks. Permission-limited evidence remains Unknown instead of being guessed.

## SYNTAX
### Type (Default)
```powershell
Test-EVXReadiness [-Type] <EventType[]> [-ActiveDirectory <EventTargetDiscoveryScope>] [-Name <string>] [-IncludeTrustedForests] [-Collector <string>] [-SubscriptionName <string>] [-ExpectedSource <string[]>] [-DirectoryCredential <pscredential>] [-EventLogCredential <pscredential>] [-Authentication <EventLogAuthentication>] [-DiscoveryTimeoutMs <int>] [-MaximumDomainCount <int>] [-MaximumTargetCount <int>] [-ProbeTimeoutMs <int>] [-MaxEventsToScan <int>] [<CommonParameters>]
```

### Scenario
```powershell
Test-EVXReadiness [-Scenario] <EventReadinessScenario> [-ActiveDirectory <EventTargetDiscoveryScope>] [-Name <string>] [-IncludeTrustedForests] [-Collector <string>] [-SubscriptionName <string>] [-ExpectedSource <string[]>] [-DirectoryCredential <pscredential>] [-EventLogCredential <pscredential>] [-Authentication <EventLogAuthentication>] [-DiscoveryTimeoutMs <int>] [-MaximumDomainCount <int>] [-MaximumTargetCount <int>] [-ProbeTimeoutMs <int>] [-MaxEventsToScan <int>] [<CommonParameters>]
```

## DESCRIPTION
Assesses EventViewerX prerequisites without changing Windows configuration.

Composes explicit target discovery, native Event Log probes, effective local audit policy, observed-event evidence, and safe provider configuration checks. Permission-limited evidence remains Unknown instead of being guessed.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EVXReadiness -Type ADUserLogonNTLMv1, KerberosServiceTicket
```

No Active Directory discovery occurs when a target is omitted.

### EXAMPLE 2
```powershell
Test-EVXReadiness -Scenario DailyActiveDirectoryReport -ActiveDirectory CurrentForest
```

Each discovered domain and domain controller retains its own success or failure evidence.

### EXAMPLE 3
```powershell
Test-EVXReadiness -Scenario AuthenticationMonitoring -Collector WEC01 -SubscriptionName EventViewerX-Authentication -ActiveDirectory CurrentForest
```

Queries ForwardedEvents and, when run locally on the collector, compares explicitly discovered DCs with subscription runtime enrollment.

## PARAMETERS

### -ActiveDirectory
Explicit directory discovery scope. The default is LocalMachine.

```yaml
Type: EventTargetDiscoveryScope
Parameter Sets: Type, Scenario
Aliases: None
Possible values: LocalMachine, CurrentDomain, CurrentForest, Domain, Forest

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Authentication
Authentication package for remote Event Log probes.

```yaml
Type: EventLogAuthentication
Parameter Sets: Type, Scenario
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Collector
Windows Event Collector assessed instead of direct source computers.

```yaml
Type: String
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DirectoryCredential
Credential used only for named directory discovery.

```yaml
Type: PSCredential
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DiscoveryTimeoutMs
Total directory discovery budget in milliseconds.

```yaml
Type: Int32
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventLogCredential
Credential used only for remote Event Log probes.

```yaml
Type: PSCredential
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExpectedSource
Additional expected source computers for WEC runtime coverage.

```yaml
Type: String[]
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeTrustedForests
Traverses forest trusts only when forest discovery was explicitly selected.

```yaml
Type: SwitchParameter
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEventsToScan
Maximum records inspected by each probe.

```yaml
Type: Int32
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumDomainCount
Maximum domains retained by one explicit discovery.

```yaml
Type: Int32
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumTargetCount
Maximum distinct event-log targets retained by one explicit discovery.

```yaml
Type: Int32
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
DNS name required by Domain or Forest scope.

```yaml
Type: String
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProbeTimeoutMs
Budget for each native Event Log probe in milliseconds.

```yaml
Type: Int32
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scenario
Built-in workflow scenario to assess.

```yaml
Type: EventReadinessScenario
Parameter Sets: Scenario
Aliases: None
Possible values: None, DailyActiveDirectoryReport, AccountLockoutMonitoring, GroupPolicyMonitoring, AuthenticationMonitoring, SecurityMonitoring

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubscriptionName
WEC subscription inspected for configuration and runtime source coverage.

```yaml
Type: String
Parameter Sets: Type, Scenario
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Explicit event types to assess.

```yaml
Type: EventType[]
Parameter Sets: Type
Aliases: None
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth, ScheduledTaskEnabled, ScheduledTaskDisabled, ScheduledTaskUpdated, FirewallRuleAdded, FirewallRuleDeleted, DefenderThreatDetected, DefenderThreatAction, DefenderConfigurationChanged, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity, AuthenticationHealth, GroupPolicyDirectoryAudit

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.EventReadinessReport`

## RELATED LINKS

- None
