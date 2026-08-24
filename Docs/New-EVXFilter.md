---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# New-EVXFilter
## SYNOPSIS
Creates a reusable typed Windows Event Log filter or compiles it to native query text.

The default output is EventViewerX.EventFilter for native event metadata. Supply Type or Definition to discover typed domain fields and build a reusable EventPredicate. Use AsXPath, LogName, or Path when native query text is required by Get-WinEvent, Event Viewer, or WEC.

## SYNTAX
### Object (Default)
```powershell
New-EVXFilter [-EventId <int[]>] [-RecordId <long[]>] [-ProviderName <string[]>] [-Level <Level[]>] [-Keywords <long[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-Data <string[]>] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-ExcludeEventId <int[]>] [<CommonParameters>]
```

### Type
```powershell
New-EVXFilter -Type <EventType> [-Where <Object>] [-Explain] [<CommonParameters>]
```

### Definition
```powershell
New-EVXFilter -Definition <Object> [-Where <Object>] [-Explain] [<CommonParameters>]
```

### XPath
```powershell
New-EVXFilter -AsXPath [-EventId <int[]>] [-RecordId <long[]>] [-ProviderName <string[]>] [-Level <Level[]>] [-Keywords <long[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-Data <string[]>] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-ExcludeEventId <int[]>] [<CommonParameters>]
```

### ChannelXml
```powershell
New-EVXFilter [-LogName] <string[]> [-EventId <int[]>] [-RecordId <long[]>] [-ProviderName <string[]>] [-Level <Level[]>] [-Keywords <long[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-Data <string[]>] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-ExcludeEventId <int[]>] [<CommonParameters>]
```

### FileXml
```powershell
New-EVXFilter [-Path] <string[]> [-EventId <int[]>] [-RecordId <long[]>] [-ProviderName <string[]>] [-Level <Level[]>] [-Keywords <long[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-Data <string[]>] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-ExcludeEventId <int[]>] [<CommonParameters>]
```

## DESCRIPTION
Creates a reusable typed Windows Event Log filter or compiles it to native query text.

The default output is EventViewerX.EventFilter for native event metadata. Supply Type or Definition to discover typed domain fields and build a reusable EventPredicate. Use AsXPath, LogName, or Path when native query text is required by Get-WinEvent, Event Viewer, or WEC.

## EXAMPLES

### EXAMPLE 1
```powershell
$filter = New-EVXFilter -EventId 4625 -TimePeriod LastDay
```

Returns an EventFilter rather than opaque query text.

### EXAMPLE 2
```powershell
Get-WinEvent -LogName Security -FilterXPath (New-EVXFilter -EventId 4625 -AsXPath)
```

Compiles the same typed filter to native XPath.

### EXAMPLE 3
```powershell
New-EVXFilter -LogName Security -EventId 4625 -NamedDataExcludeFilter @{ TargetUserName = 'svc_legacy' }
```

Returns QueryList XML with native Select and Suppress clauses.

### EXAMPLE 4
```powershell
$filter = New-EVXFilter -Type ADUserLogonFailed; $filter.AllOf($filter.Fields.Who.In('EVOTEC\Alice', 'EVOTEC\Bob'), $filter.Fields.IPAddress.MatchesSubnet('10.0.0.0/8')); Get-EVXEvent -Filter $filter -TimePeriod Last7Days
```

The builder retains both the typed definition and selected predicate, so Filter is sufficient to execute the query.

### EXAMPLE 5
```powershell
New-EVXFilter -Type ADUserLogonFailed -Where { $_.Who -like 'EVOTEC\*' } -Explain
```

Returns native and managed predicate stages without reading events.

## PARAMETERS

### -AsXPath
Returns one native XPath expression.

```yaml
Type: SwitchParameter
Parameter Sets: XPath
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Data
Unnamed EventData values to include.

```yaml
Type: String[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Definition
Custom EventDefinition instance or JSON file whose typed fields should be exposed.

```yaml
Type: Object
Parameter Sets: Definition
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Absolute end of the time range.

```yaml
Type: DateTime
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: DateTo
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventId
Event identifiers to include.

```yaml
Type: Int32[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: Id
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExcludeEventId
Event identifiers to suppress.

```yaml
Type: Int32[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: ExcludeId
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Explain
Returns the native and managed execution plan for Where instead of the reusable filter.

```yaml
Type: SwitchParameter
Parameter Sets: Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Keywords
Windows keyword masks to include.

```yaml
Type: Int64[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Level
Numeric Windows event levels to include.

```yaml
Type: Level[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values: LogAlways, Critical, Error, Warning, Informational, Verbose

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Channel used to produce QueryList XML.

```yaml
Type: String[]
Parameter Sets: ChannelXml
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NamedDataExcludeFilter
Named EventData values to suppress.

```yaml
Type: Hashtable
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NamedDataFilter
Named EventData values to include.

```yaml
Type: Hashtable
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Offline event-log files used to produce QueryList XML.

```yaml
Type: String[]
Parameter Sets: FileXml
Aliases: PSPath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProviderName
Provider names to include.

```yaml
Type: String[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecordId
Event record identifiers to include.

```yaml
Type: Int64[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: EventRecordId
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Absolute beginning of the time range.

```yaml
Type: DateTime
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: DateFrom
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimePeriod
Named relative time range.

```yaml
Type: TimePeriod
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values: PastHour, CurrentHour, PastDay, CurrentDay, PastMonth, CurrentMonth, PastQuarter, CurrentQuarter, Last3Days, Last7Days, Last14Days, Last1Hour, Last2Hours, Last3Hours, Last6Hours, Last12Hours, Last16Hours, Last24Hours, Today, Yesterday, Everything, TillLastMonday, TillLastTuesday, TillLastWednesday, TillLastThursday, TillLastFriday, TillLastSaturday, TillLastSunday, Last15Minutes, Last30Minutes

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Built-in event type whose typed fields should be exposed for predicate construction.

```yaml
Type: EventType
Parameter Sets: Type
Aliases: None
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth, ScheduledTaskEnabled, ScheduledTaskDisabled, ScheduledTaskUpdated, FirewallRuleAdded, FirewallRuleDeleted, DefenderThreatDetected, DefenderThreatAction, DefenderConfigurationChanged, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity, AuthenticationHealth, GroupPolicyDirectoryAudit

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserId
User security identifiers to include.

```yaml
Type: String[]
Parameter Sets: Object, XPath, ChannelXml, FileXml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Where
Optional restricted typed predicate expression stored in the returned reusable filter.

```yaml
Type: Object
Parameter Sets: Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.EventFilter`
- `System.String`
- `PSEventViewer.PowerShellEventPredicateBuilder`: PowerShell-friendly view over the canonical EventViewerX typed predicate builder.

## RELATED LINKS

- None
