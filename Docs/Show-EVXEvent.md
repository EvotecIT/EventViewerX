---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Show-EVXEvent
## SYNOPSIS
Queries or accepts EventViewerX events and creates polished HTML, Excel, or email output.

Show-EVXEvent uses one normalized report snapshot for every selected output. A Type owns its source channels and event IDs; LogName is reserved for generic event queries.

Typed and custom definitions render only their domain fields. Composite types keep each leaf schema in a separate table and Excel worksheet, while Event Provenance retains the technical Windows event context.

## SYNTAX
### Input (Default)
```powershell
Show-EVXEvent -InputObject <Object> [-Title <string>] [-HtmlPath <string>] [-DrawerPlacement <MonitoringRecordDrawerPlacement>] [-ExcelPath <string>] [-CsvPath <string>] [-StorePath <string>] [-EmailPackage] [-Open] [-PassThru] [-DuplicateMode <EventDuplicateMode>] [-OccurrenceWindow <timespan>] [-MaximumOccurrenceObservations <int>] [-MaximumOccurrenceGroups <int>] [<CommonParameters>]
```

### Type
```powershell
Show-EVXEvent [-Type] <EventType[]> [-Path <string[]>] [-Where <Object>] [-EventRecordId <long[]>] [-MachineName <string[]>] [-Collector <string[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-MaxConcurrency <int>] [-Oldest] [-ResolveDns] [-ContextStorePath <string>] [-ContextAuthorization <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Title <string>] [-HtmlPath <string>] [-DrawerPlacement <MonitoringRecordDrawerPlacement>] [-ExcelPath <string>] [-CsvPath <string>] [-StorePath <string>] [-EmailPackage] [-Open] [-PassThru] [-DuplicateMode <EventDuplicateMode>] [-OccurrenceWindow <timespan>] [-MaximumOccurrenceObservations <int>] [-MaximumOccurrenceGroups <int>] [<CommonParameters>]
```

### Store
```powershell
Show-EVXEvent [-FromStore] <string> [-Type <EventType[]>] [-LogName <string>] [-Definition <Object>] [-Where <Object>] [-EventId <int[]>] [-EventRecordId <long[]>] [-SourceComputer <string[]>] [-ProviderName <string[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-Oldest] [-SummaryPeriod <EventStoreSummaryPeriod>] [-Title <string>] [-HtmlPath <string>] [-DrawerPlacement <MonitoringRecordDrawerPlacement>] [-ExcelPath <string>] [-CsvPath <string>] [-StorePath <string>] [-EmailPackage] [-Open] [-PassThru] [-DuplicateMode <EventDuplicateMode>] [-OccurrenceWindow <timespan>] [-MaximumOccurrenceObservations <int>] [-MaximumOccurrenceGroups <int>] [<CommonParameters>]
```

### Log
```powershell
Show-EVXEvent [-LogName] <string> [-EventId <int[]>] [-EventRecordId <long[]>] [-MachineName <string[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEvents <long>] [-MaxConcurrency <int>] [-Oldest] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Title <string>] [-HtmlPath <string>] [-DrawerPlacement <MonitoringRecordDrawerPlacement>] [-ExcelPath <string>] [-CsvPath <string>] [-StorePath <string>] [-EmailPackage] [-Open] [-PassThru] [-DuplicateMode <EventDuplicateMode>] [-OccurrenceWindow <timespan>] [-MaximumOccurrenceObservations <int>] [-MaximumOccurrenceGroups <int>] [<CommonParameters>]
```

### Path
```powershell
Show-EVXEvent [-Path] <string[]> [-EventId <int[]>] [-EventRecordId <long[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEvents <long>] [-MaxConcurrency <int>] [-Oldest] [-Title <string>] [-HtmlPath <string>] [-DrawerPlacement <MonitoringRecordDrawerPlacement>] [-ExcelPath <string>] [-CsvPath <string>] [-StorePath <string>] [-EmailPackage] [-Open] [-PassThru] [-DuplicateMode <EventDuplicateMode>] [-OccurrenceWindow <timespan>] [-MaximumOccurrenceObservations <int>] [-MaximumOccurrenceGroups <int>] [<CommonParameters>]
```

### Definition
```powershell
Show-EVXEvent [-Definition] <Object> [-Path <string[]>] [-Where <Object>] [-EventRecordId <long[]>] [-MachineName <string[]>] [-Collector <string[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-MaxConcurrency <int>] [-Oldest] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Title <string>] [-HtmlPath <string>] [-DrawerPlacement <MonitoringRecordDrawerPlacement>] [-ExcelPath <string>] [-CsvPath <string>] [-StorePath <string>] [-EmailPackage] [-Open] [-PassThru] [-DuplicateMode <EventDuplicateMode>] [-OccurrenceWindow <timespan>] [-MaximumOccurrenceObservations <int>] [-MaximumOccurrenceGroups <int>] [<CommonParameters>]
```

## DESCRIPTION
Queries or accepts EventViewerX events and creates polished HTML, Excel, or email output.

Show-EVXEvent uses one normalized report snapshot for every selected output. A Type owns its source channels and event IDs; LogName is reserved for generic event queries.

Typed and custom definitions render only their domain fields. Composite types keep each leaf schema in a separate table and Excel worksheet, while Event Provenance retains the technical Windows event context.

## EXAMPLES

### EXAMPLE 1
```powershell
Show-EVXEvent -Type ADUserLogonFailed -TimePeriod Last24Hours
```

Queries the definition-owned Security events and opens a self-contained interactive HTML report.

### EXAMPLE 2
```powershell
$filter = New-EVXFilter -Type ActiveDirectoryAuthentication; Show-EVXEvent -Type ActiveDirectoryAuthentication -Where $filter.Fields.Who.Contains('svc-') -Collector WEC01 -HtmlPath .\Auth.html -ExcelPath .\Auth.xlsx -PassThru
```

Reads ForwardedEvents once and renders both formats from the same snapshot.

### EXAMPLE 3
```powershell
Get-EVXEvent -LogName System -EventId 41,6008 | Show-EVXEvent -HtmlPath .\Startup.html
```

Does not query the event log again.

## PARAMETERS

### -Authentication
Remote Windows Event Log authentication package.

```yaml
Type: EventLogAuthentication
Parameter Sets: Type, Log, Definition
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Collector
Windows Event Collector targets. Typed source channels are matched inside ForwardedEvents.

```yaml
Type: String[]
Parameter Sets: Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContextAuthorization
Caller-authorized partition used to resolve non-shareable imported Group Policy context. Requires ContextStorePath.

```yaml
Type: String
Parameter Sets: Type
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContextStorePath
SQLite file that preserves Group Policy names and identity history across report runs.
This is available only with -Type GroupPolicyDirectoryAudit and never performs directory discovery.

```yaml
Type: String
Parameter Sets: Type
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Remote query credential.

```yaml
Type: PSCredential
Parameter Sets: Type, Log, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CsvPath
Homogeneous CSV path, or a .zip bundle path when the report contains multiple typed schemas.

```yaml
Type: String
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Definition
Custom JSON definition path or an EventDefinition instance.

```yaml
Type: Object
Parameter Sets: Store, Definition
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DrawerPlacement
Preferred location of the selected-record drawer in interactive HTML output.

```yaml
Type: MonitoringRecordDrawerPlacement
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values: Auto, Top, Right

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DuplicateMode
Optional exact transport or compiled semantic occurrence grouping.

```yaml
Type: EventDuplicateMode
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values: None, Transport, Semantic

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EmailPackage
Returns a responsive transport-neutral email package for Mailozaurr.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Absolute end time.

```yaml
Type: DateTime
Parameter Sets: Type, Store, Log, Path, Definition
Aliases: DateTo
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventId
Optional event IDs for a generic LogName query.

```yaml
Type: Int32[]
Parameter Sets: Store, Log, Path
Aliases: Id
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventRecordId
Exact event record identifiers, including IDs passed by an event-triggered scheduled task.

```yaml
Type: Int64[]
Parameter Sets: Type, Store, Log, Path, Definition
Aliases: RecordId
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExcelPath
Excel workbook output path.

```yaml
Type: String
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FromStore
Reads normalized rows from a local EventViewerX SQLite store instead of querying event logs.

```yaml
Type: String
Parameter Sets: Store
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HtmlPath
Self-contained interactive HTML output path.

```yaml
Type: String
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Existing EventObject or EventTypeRecord values. No source query is performed.

```yaml
Type: Object
Parameter Sets: Input
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -LogName
Generic event channel. Mutually exclusive with Type.

```yaml
Type: String
Parameter Sets: Store, Log
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Direct local or remote query targets.

```yaml
Type: String[]
Parameter Sets: Type, Log, Definition
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrency
Maximum sources opened concurrently.

```yaml
Type: Int32
Parameter Sets: Type, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEvents
Maximum report rows. Zero is unlimited.

```yaml
Type: Int64
Parameter Sets: Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEventsScanned
Maximum raw candidates evaluated before exact predicate verification. Stored queries default to 100,000 when omitted; zero is unlimited.

```yaml
Type: Int64
Parameter Sets: Type, Store, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumOccurrenceGroups
Maximum occurrence groups retained.

```yaml
Type: Int32
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumOccurrenceObservations
Maximum observations accepted by one occurrence grouping operation.

```yaml
Type: Int32
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OccurrenceWindow
Maximum separation for compiled semantic causal-identifier grouping.

```yaml
Type: TimeSpan
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Oldest
Reads oldest matches first.

```yaml
Type: SwitchParameter
Parameter Sets: Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Open
Opens generated files with the registered desktop applications.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Returns the normalized report snapshot in addition to generated output.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
One or more offline EVTX files. Type or Definition may be supplied to apply typed semantics; Path alone creates a generic report.

```yaml
Type: String[]
Parameter Sets: Type, Path, Definition
Aliases: PSPath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -ProviderName
Provider names used to filter stored rows.

```yaml
Type: String[]
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolveDns
Enriches typed IP-address properties through DnsClientX.

```yaml
Type: SwitchParameter
Parameter Sets: Type
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceComputer
Original source computers used to filter stored rows.

```yaml
Type: String[]
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Absolute start time.

```yaml
Type: DateTime
Parameter Sets: Type, Store, Log, Path, Definition
Aliases: DateFrom
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StorePath
Persists the normalized report rows in an optional local EventViewerX SQLite store.

```yaml
Type: String
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SummaryPeriod
Groups stored events into an hourly, daily, weekly, or monthly report.

```yaml
Type: EventStoreSummaryPeriod
Parameter Sets: Store
Aliases: None
Possible values: Hour, Day, Week, Month

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimePeriod
Relative time window.

```yaml
Type: TimePeriod
Parameter Sets: Type, Store, Log, Path, Definition
Aliases: None
Possible values: PastHour, CurrentHour, PastDay, CurrentDay, PastMonth, CurrentMonth, PastQuarter, CurrentQuarter, Last3Days, Last7Days, Last14Days, Last1Hour, Last2Hours, Last3Hours, Last6Hours, Last12Hours, Last16Hours, Last24Hours, Today, Yesterday, Everything, TillLastMonday, TillLastTuesday, TillLastWednesday, TillLastThursday, TillLastFriday, TillLastSaturday, TillLastSunday, Last15Minutes, Last30Minutes

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Report title.

```yaml
Type: String
Parameter Sets: Input, Type, Store, Log, Path, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Built-in leaf or composite event definitions. Each definition owns its channels and event IDs.

```yaml
Type: EventType[]
Parameter Sets: Type, Store
Aliases: None
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth, ScheduledTaskEnabled, ScheduledTaskDisabled, ScheduledTaskUpdated, FirewallRuleAdded, FirewallRuleDeleted, DefenderThreatDetected, DefenderThreatAction, DefenderConfigurationChanged, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity, AuthenticationHealth, GroupPolicyDirectoryAudit

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Where
Reusable typed EventPredicate, restricted ScriptBlock, predicate JSON, or predicate JSON file.

```yaml
Type: Object
Parameter Sets: Type, Store, Definition
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

- `System.String[]`
- `System.Object`

## OUTPUTS

- `EventViewerX.Reporting.EventReport`
- `EventViewerX.Reporting.EventAggregationResult`
- `EventViewerX.Reporting.EventOccurrenceResult`
- `EventViewerX.Reporting.EventEmailPackage`

## RELATED LINKS

- None
