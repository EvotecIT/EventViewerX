---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Start-EVXWatcher
## SYNOPSIS
Starts real-time monitoring of Windows Event Logs with customizable filters and actions.

Supports explicit event IDs or EventType, provider-side filtering, optional staging events, auto-stop conditions, and a callback for each match.

## SYNTAX
### EventId (Default)
```powershell
Start-EVXWatcher [-LogName] <string> [-EventId] <int[]> [-Action] <scriptblock> [-MachineName <string>] [-Staging] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Start <EventLogSubscriptionStart>] [-BookmarkXml <string>] [-IgnoreStaleBookmark] [-TolerateQueryErrors] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-BufferCapacity <int>] [-SessionTimeoutMs <int>] [-Name <string>] [-ActionIdentity <string>] [-TimeOut <TimeSpan>] [-StopOnMatch] [-StopAfter <int>] [<CommonParameters>]
```

### FilterHashtable
```powershell
Start-EVXWatcher [-LogName] <string> [-FilterHashtable] <hashtable> [-Action] <scriptblock> [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Start <EventLogSubscriptionStart>] [-BookmarkXml <string>] [-IgnoreStaleBookmark] [-TolerateQueryErrors] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-BufferCapacity <int>] [-SessionTimeoutMs <int>] [-Name <string>] [-ActionIdentity <string>] [-TimeOut <TimeSpan>] [-StopOnMatch] [-StopAfter <int>] [<CommonParameters>]
```

### Filter
```powershell
Start-EVXWatcher [-LogName] <string> [-Filter] <EventFilter> [-Action] <scriptblock> [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Start <EventLogSubscriptionStart>] [-BookmarkXml <string>] [-IgnoreStaleBookmark] [-TolerateQueryErrors] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-BufferCapacity <int>] [-SessionTimeoutMs <int>] [-Name <string>] [-ActionIdentity <string>] [-TimeOut <TimeSpan>] [-StopOnMatch] [-StopAfter <int>] [<CommonParameters>]
```

### FilterXPath
```powershell
Start-EVXWatcher [-LogName] <string> [-FilterXPath] <string> [-Action] <scriptblock> [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Start <EventLogSubscriptionStart>] [-BookmarkXml <string>] [-IgnoreStaleBookmark] [-TolerateQueryErrors] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-BufferCapacity <int>] [-SessionTimeoutMs <int>] [-Name <string>] [-ActionIdentity <string>] [-TimeOut <TimeSpan>] [-StopOnMatch] [-StopAfter <int>] [<CommonParameters>]
```

### Type
```powershell
Start-EVXWatcher [-Type] <EventType[]> [-Action] <scriptblock> [-MachineName <string>] [-Collector <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Start <EventLogSubscriptionStart>] [-BookmarkXml <string>] [-IgnoreStaleBookmark] [-TolerateQueryErrors] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-BufferCapacity <int>] [-SessionTimeoutMs <int>] [-Name <string>] [-ActionIdentity <string>] [-TimeOut <TimeSpan>] [-StopOnMatch] [-StopAfter <int>] [<CommonParameters>]
```

### Definition
```powershell
Start-EVXWatcher [-Definition] <Object> [-Action] <scriptblock> [-MachineName <string>] [-Collector <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Start <EventLogSubscriptionStart>] [-BookmarkXml <string>] [-IgnoreStaleBookmark] [-TolerateQueryErrors] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-BufferCapacity <int>] [-SessionTimeoutMs <int>] [-Name <string>] [-ActionIdentity <string>] [-TimeOut <TimeSpan>] [-StopOnMatch] [-StopAfter <int>] [<CommonParameters>]
```

## DESCRIPTION
Starts real-time monitoring of Windows Event Logs with customizable filters and actions.

Supports explicit event IDs or EventType, provider-side filtering, optional staging events, auto-stop conditions, and a callback for each match.

## EXAMPLES

### EXAMPLE 1
```powershell
Start-EVXWatcher -MachineName DC1 -LogName Security -EventId 4625 -Action { Write-Host "Failed logon:" $_.MessageSubject }
```

Streams failed logons and prints a summary.

### EXAMPLE 2
```powershell
Start-EVXWatcher -MachineName DC1 -Type ADUserLockouts -Action { $_ | Write-Output }
```

Triggers an alert when any AD lockout occurs.

### EXAMPLE 3
```powershell
Start-EVXWatcher -MachineName SRV1 -LogName System -EventId 41 -StopOnMatch -Action { $_ | Out-File crash.txt }
```

Captures the first critical kernel-power event then exits.

### EXAMPLE 4
```powershell
Start-EVXWatcher -MachineName SRV1 -LogName Application -EventId 1000 -TimeOut (New-TimeSpan -Minutes 15) -Action { $_.WriteToHost() }
```

Watches for 15 minutes and then stops automatically.

## PARAMETERS

### -Action
Script block executed when matching events are detected.

```yaml
Type: ScriptBlock
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ActionIdentity
Stable caller-defined identity used to reuse a named watcher across recreated host delegates.
Omit this parameter to reject reuse when the action delegate is not the same instance.

```yaml
Type: String
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Authentication
Authentication package used for a remote subscription.

```yaml
Type: EventLogAuthentication
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BookmarkXml
Native bookmark XML used with Start=AfterBookmark.

```yaml
Type: String
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BufferCapacity
Maximum detached snapshots buffered before delivery stops rather than dropping data.

```yaml
Type: Int32
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Collector
Windows Event Collector computer whose ForwardedEvents channel should be monitored.

```yaml
Type: String
Parameter Sets: Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials used for a remote native subscription.

```yaml
Type: PSCredential
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Definition
Custom EventViewerX definition instance or JSON file path.

```yaml
Type: Object
Parameter Sets: Definition
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventId
Array of event identifiers to monitor.

```yaml
Type: Int32[]
Parameter Sets: EventId
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FallbackMessageCulture
Fallback culture when the primary provider resources are unavailable.

```yaml
Type: CultureInfo
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Filter
Reusable typed filter produced by New-EVXFilter or EventViewerX.

```yaml
Type: EventFilter
Parameter Sets: Filter
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilterHashtable
Event predicates using the same keys as Get-EVXEvent -FilterHashtable.
LogName and Path are not included because this watcher targets one LogName.

```yaml
Type: Hashtable
Parameter Sets: FilterHashtable
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilterXPath
Native Windows Event Log XPath applied by the subscription.

```yaml
Type: String
Parameter Sets: FilterXPath
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IgnoreStaleBookmark
Allows a stale bookmark to resume from the closest available record.

```yaml
Type: SwitchParameter
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Name of the log to watch on the specified machine.

```yaml
Type: String
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Optional computer to monitor. The local computer is used by default.

```yaml
Type: String
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageCulture
Primary culture for message and provider-label rendering.

```yaml
Type: CultureInfo
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Optional name for the watcher instance.

```yaml
Type: String
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReadMode
Amount of event data projected for every delivered event.

```yaml
Type: EventReadMode
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values: Metadata, Message, StructuredData, RawXml, Full, StructuredDataAndMessage

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SessionTimeoutMs
Remote native session connection timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Staging
Enables staging mode which also watches for event ID 350.

```yaml
Type: SwitchParameter
Parameter Sets: EventId
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Start
Future, Oldest, or AfterBookmark subscription starting position.

```yaml
Type: EventLogSubscriptionStart
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values: Future, Oldest, AfterBookmark

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StopAfter
Stops watching after processing the specified number of events.

```yaml
Type: Int32
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StopOnMatch
When set, the watcher stops after the first matching event.

```yaml
Type: SwitchParameter
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeOut
Duration after which the watcher stops automatically.

```yaml
Type: TimeSpan
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TolerateQueryErrors
Allows Windows to tolerate query errors where the native API supports it.

```yaml
Type: SwitchParameter
Parameter Sets: EventId, FilterHashtable, Filter, FilterXPath, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
One or more built-in typed event definitions to monitor.

```yaml
Type: EventType[]
Parameter Sets: Type
Aliases: NamedEvent, NamedEvents
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, KerberosKdcRc4Audit, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth, ScheduledTaskEnabled, ScheduledTaskDisabled, ScheduledTaskUpdated, FirewallRuleAdded, FirewallRuleDeleted, DefenderThreatDetected, DefenderThreatAction, DefenderConfigurationChanged, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity, AuthenticationHealth, GroupPolicyDirectoryAudit

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.WatcherInfo`

## RELATED LINKS

- None
