---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# New-EVXCollectorSubscription
## SYNOPSIS
Creates a typed collector- or source-initiated WEC subscription definition.

Builds safe Windows Event Collector XML from typed reports, custom definitions, a QueryList, or common event filters. The command does not change the collector; pipe the definition to Set-EVXCollectorSubscription to apply it.

## SYNTAX
### Filter (Default)
```powershell
New-EVXCollectorSubscription [-Name] <string> [[-SourceComputer] <string[]>] [-LogName] <string> [-SubscriptionType <CollectorSubscriptionType>] [-CollectorHostName <string>] [-AllowedSourceDomainComputersSddl <string>] [-AllowedSourceSid <string[]>] [-SourceRefreshIntervalSeconds <int>] [-EventId <int[]>] [-ProviderName <string[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-Description <string>] [-Enabled <bool>] [-ReadExistingEvents] [-DeliveryMode <CollectorSubscriptionDeliveryMode>] [-MaxItems <int>] [-MaxLatencyMilliseconds <int>] [-HeartbeatIntervalMilliseconds <int>] [-TransportName <string>] [-TransportPort <int>] [-ContentFormat <CollectorSubscriptionContentFormat>] [-Locale <cultureinfo>] [-DestinationLog <string>] [-PublisherName <string>] [-OutputPath <string>] [-Force] [-PassThru] [<CommonParameters>]
```

### TypedFilter
```powershell
New-EVXCollectorSubscription [-Name] <string> [[-SourceComputer] <string[]>] [-LogName] <string> -Filter <EventFilter> [-SubscriptionType <CollectorSubscriptionType>] [-CollectorHostName <string>] [-AllowedSourceDomainComputersSddl <string>] [-AllowedSourceSid <string[]>] [-SourceRefreshIntervalSeconds <int>] [-Description <string>] [-Enabled <bool>] [-ReadExistingEvents] [-DeliveryMode <CollectorSubscriptionDeliveryMode>] [-MaxItems <int>] [-MaxLatencyMilliseconds <int>] [-HeartbeatIntervalMilliseconds <int>] [-TransportName <string>] [-TransportPort <int>] [-ContentFormat <CollectorSubscriptionContentFormat>] [-Locale <cultureinfo>] [-DestinationLog <string>] [-PublisherName <string>] [-OutputPath <string>] [-Force] [-PassThru] [<CommonParameters>]
```

### Type
```powershell
New-EVXCollectorSubscription [-Name] <string> [[-SourceComputer] <string[]>] [-Type] <EventType[]> [-SubscriptionType <CollectorSubscriptionType>] [-CollectorHostName <string>] [-AllowedSourceDomainComputersSddl <string>] [-AllowedSourceSid <string[]>] [-SourceRefreshIntervalSeconds <int>] [-Description <string>] [-Enabled <bool>] [-ReadExistingEvents] [-DeliveryMode <CollectorSubscriptionDeliveryMode>] [-MaxItems <int>] [-MaxLatencyMilliseconds <int>] [-HeartbeatIntervalMilliseconds <int>] [-TransportName <string>] [-TransportPort <int>] [-ContentFormat <CollectorSubscriptionContentFormat>] [-Locale <cultureinfo>] [-DestinationLog <string>] [-PublisherName <string>] [-OutputPath <string>] [-Force] [-PassThru] [<CommonParameters>]
```

### Definition
```powershell
New-EVXCollectorSubscription [-Name] <string> [[-SourceComputer] <string[]>] [-Definition] <Object> [-SubscriptionType <CollectorSubscriptionType>] [-CollectorHostName <string>] [-AllowedSourceDomainComputersSddl <string>] [-AllowedSourceSid <string[]>] [-SourceRefreshIntervalSeconds <int>] [-Description <string>] [-Enabled <bool>] [-ReadExistingEvents] [-DeliveryMode <CollectorSubscriptionDeliveryMode>] [-MaxItems <int>] [-MaxLatencyMilliseconds <int>] [-HeartbeatIntervalMilliseconds <int>] [-TransportName <string>] [-TransportPort <int>] [-ContentFormat <CollectorSubscriptionContentFormat>] [-Locale <cultureinfo>] [-DestinationLog <string>] [-PublisherName <string>] [-OutputPath <string>] [-Force] [-PassThru] [<CommonParameters>]
```

### QueryXml
```powershell
New-EVXCollectorSubscription [-Name] <string> [[-SourceComputer] <string[]>] [-QueryXml] <string> [-SubscriptionType <CollectorSubscriptionType>] [-CollectorHostName <string>] [-AllowedSourceDomainComputersSddl <string>] [-AllowedSourceSid <string[]>] [-SourceRefreshIntervalSeconds <int>] [-Description <string>] [-Enabled <bool>] [-ReadExistingEvents] [-DeliveryMode <CollectorSubscriptionDeliveryMode>] [-MaxItems <int>] [-MaxLatencyMilliseconds <int>] [-HeartbeatIntervalMilliseconds <int>] [-TransportName <string>] [-TransportPort <int>] [-ContentFormat <CollectorSubscriptionContentFormat>] [-Locale <cultureinfo>] [-DestinationLog <string>] [-PublisherName <string>] [-OutputPath <string>] [-Force] [-PassThru] [<CommonParameters>]
```

## DESCRIPTION
Creates a typed collector- or source-initiated WEC subscription definition.

Builds safe Windows Event Collector XML from typed reports, custom definitions, a QueryList, or common event filters. The command does not change the collector; pipe the definition to Set-EVXCollectorSubscription to apply it.

## EXAMPLES

### EXAMPLE 1
```powershell
New-EVXCollectorSubscription -Name FailedLogons -SourceComputer DC1,DC2 -LogName Security -EventId 4625 | Set-EVXCollectorSubscription
```

Builds a typed definition and creates or updates the local collector subscription.

### EXAMPLE 2
```powershell
New-EVXCollectorSubscription -Name GpoAudit -SubscriptionType SourceInitiated -CollectorHostName WEC01.contoso.com -Type GroupPolicyActivity -AllowedSourceSid $domainControllersSid | Set-EVXCollectorSubscription -InitializeCollector
```

Uses source policy for discovery. Domain controllers need the Domain Controllers SID or explicit computer SIDs in the source authorization SDDL.

### EXAMPLE 3
```powershell
New-EVXCollectorSubscription -Name SystemErrors -SourceComputer SRV01 -LogName System -Level Error -Enabled $false -OutputPath .\SystemErrors.xml
```

Writes inbox-compatible XML without changing the collector.

## PARAMETERS

### -AllowedSourceDomainComputersSddl
Source authorization SDDL used by a source-initiated subscription.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowedSourceSid
Explicit computer or group SIDs authorized for source-initiated forwarding. This is a simpler alternative to AllowedSourceDomainComputersSddl.

```yaml
Type: String[]
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CollectorHostName
Collector DNS name required for Push delivery and the source SubscriptionManager policy value.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContentFormat
Raw Events or RenderedText delivery.

```yaml
Type: CollectorSubscriptionContentFormat
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values: Events, RenderedText

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Definition
Custom typed definition or JSON definition path.

```yaml
Type: Object
Parameter Sets: Definition
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryMode
Pull or push delivery.

```yaml
Type: CollectorSubscriptionDeliveryMode
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values: Pull, Push

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Description
Operator-facing description.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DestinationLog
Collector destination channel.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Enabled
Whether the subscription starts enabled.

```yaml
Type: Boolean
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Latest event time included in the generated query.

```yaml
Type: DateTime
Parameter Sets: Filter
Aliases: DateTo
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventId
Event identifiers included in the generated query.

```yaml
Type: Int32[]
Parameter Sets: Filter
Aliases: Id
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Filter
Reusable typed event filter.

```yaml
Type: EventFilter
Parameter Sets: TypedFilter
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Overwrites OutputPath when it already exists.

```yaml
Type: SwitchParameter
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HeartbeatIntervalMilliseconds
Heartbeat or polling interval in milliseconds.

```yaml
Type: Int32
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Level
Numeric Windows event levels included in the generated query.

```yaml
Type: Level[]
Parameter Sets: Filter
Aliases: None
Possible values: LogAlways, Critical, Error, Warning, Informational, Verbose

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Locale
Culture used for rendered text.

```yaml
Type: CultureInfo
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Event channel used by the generated query.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxItems
Maximum items delivered in one batch.

```yaml
Type: Int32
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxLatencyMilliseconds
Maximum delivery latency in milliseconds.

```yaml
Type: Int32
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Unique WEC subscription name.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Optional path that receives the generated XML.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Also emits the typed definition when OutputPath is used.

```yaml
Type: SwitchParameter
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProviderName
Provider names included in the generated query.

```yaml
Type: String[]
Parameter Sets: Filter
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PublisherName
Publisher that owns or imports the destination channel.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -QueryXml
Complete Windows Event Log QueryList XML.

```yaml
Type: String
Parameter Sets: QueryXml
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReadExistingEvents
Whether already-recorded source events are collected.

```yaml
Type: SwitchParameter
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceComputer
Source computers collected by this subscription.

```yaml
Type: String[]
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: ComputerName, MachineName, ServerName
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceRefreshIntervalSeconds
Source policy refresh interval in seconds.

```yaml
Type: Int32
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Earliest event time included in the generated query.

```yaml
Type: DateTime
Parameter Sets: Filter
Aliases: DateFrom
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubscriptionType
CollectorInitiated for explicit sources, or SourceInitiated for policy-discovered sources.

```yaml
Type: CollectorSubscriptionType
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values: CollectorInitiated, SourceInitiated

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimePeriod
Relative time range included in the generated query.

```yaml
Type: TimePeriod
Parameter Sets: Filter
Aliases: None
Possible values: PastHour, CurrentHour, PastDay, CurrentDay, PastMonth, CurrentMonth, PastQuarter, CurrentQuarter, Last3Days, Last7Days, Last14Days, Last1Hour, Last2Hours, Last3Hours, Last6Hours, Last12Hours, Last16Hours, Last24Hours, Today, Yesterday, Everything, TillLastMonday, TillLastTuesday, TillLastWednesday, TillLastThursday, TillLastFriday, TillLastSaturday, TillLastSunday, Last15Minutes, Last30Minutes

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TransportName
HTTP or HTTPS transport.

```yaml
Type: String
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values: HTTP, HTTPS

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TransportPort
Explicit transport port. Zero uses the Windows default.

```yaml
Type: Int32
Parameter Sets: Filter, TypedFilter, Type, Definition, QueryXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Built-in leaf or composite event types. Their definitions own source channels and event IDs.

```yaml
Type: EventType[]
Parameter Sets: Type
Aliases: None
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth, ScheduledTaskEnabled, ScheduledTaskDisabled, ScheduledTaskUpdated, FirewallRuleAdded, FirewallRuleDeleted, DefenderThreatDetected, DefenderThreatAction, DefenderConfigurationChanged, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity, AuthenticationHealth, GroupPolicyDirectoryAudit

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.CollectorSubscriptionDefinition`
- `System.IO.FileInfo`

## RELATED LINKS

- None
