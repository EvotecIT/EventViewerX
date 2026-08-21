---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXEvent
## SYNOPSIS
Enhanced event querying cmdlet that replaces and extends Get-WinEvent functionality.

Supports local and remote logs, built-in event types, custom JSON definitions, record ID resumes, parallel queries, and rich filtering.

## SYNTAX
### TypedFilter (Default)
```powershell
Get-EVXEvent -Filter <Object> [-EventRecordId <long[]>] [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-Collector <List[string]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-DisableParallel] [-Explain] [-Describe] [-FallbackMessageCulture <cultureinfo>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-ContinueOnError] [-IncludeBookmark] [<CommonParameters>]
```

### Channel
```powershell
Get-EVXEvent [-LogName] <string[]> [[-EventId] <int[]>] [-EventRecordId <long[]>] [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-Keywords <long[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-DisableParallel] [-FallbackMessageCulture <cultureinfo>] [-FilterXPath <string>] [-Filter <Object>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-BookmarkXml <string>] [-BookmarkOffset <long>] [-IgnoreStaleBookmark] [-ContinueOnError] [-Force] [-IncludeBookmark] [<CommonParameters>]
```

### Path
```powershell
Get-EVXEvent -Path <string[]> [-EventId <int[]>] [-EventRecordId <long[]>] [-RecordIdFile <string>] [-RecordIdKey <string>] [-ProviderName <string[]>] [-Keywords <long[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-DisableParallel] [-Explain] [-Describe] [-FallbackMessageCulture <cultureinfo>] [-FilterXPath <string>] [-Filter <Object>] [-BookmarkXml <string>] [-BookmarkOffset <long>] [-IgnoreStaleBookmark] [-ContinueOnError] [-IncludeBookmark] [<CommonParameters>]
```

### Type
```powershell
Get-EVXEvent -Type <EventType[]> [-Path <string[]>] [-EventRecordId <long[]>] [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-Collector <List[string]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ResolveDns] [-DnsTimeoutMs <int>] [-DnsMaxConcurrency <int>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-DisableParallel] [-Where <Object>] [-Explain] [-Describe] [-FallbackMessageCulture <cultureinfo>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-ContinueOnError] [-IncludeBookmark] [<CommonParameters>]
```

### Definition
```powershell
Get-EVXEvent -Definition <Object> [-Path <string[]>] [-EventRecordId <long[]>] [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-Collector <List[string]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-DisableParallel] [-Where <Object>] [-Explain] [-Describe] [-FallbackMessageCulture <cultureinfo>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-ContinueOnError] [-IncludeBookmark] [<CommonParameters>]
```

### Provider
```powershell
Get-EVXEvent [[-EventId] <int[]>] -ProviderName <string[]> [-EventRecordId <long[]>] [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-Keywords <long[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-UserId <string[]>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-DisableParallel] [-FallbackMessageCulture <cultureinfo>] [-Filter <Object>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-BookmarkXml <string>] [-BookmarkOffset <long>] [-IgnoreStaleBookmark] [-ContinueOnError] [-Force] [-IncludeBookmark] [<CommonParameters>]
```

### Hashtable
```powershell
Get-EVXEvent [-FilterHashtable] <hashtable[]> [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-NamedDataFilter <hashtable>] [-NamedDataExcludeFilter <hashtable>] [-DisableParallel] [-FallbackMessageCulture <cultureinfo>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-BookmarkXml <string>] [-BookmarkOffset <long>] [-IgnoreStaleBookmark] [-ContinueOnError] [-TolerateQueryErrors] [-Force] [-IncludeBookmark] [<CommonParameters>]
```

### Xml
```powershell
Get-EVXEvent [-FilterXml] <xml> [-RecordIdFile <string>] [-RecordIdKey <string>] [-MachineName <List[string]>] [-MessageRegex <regex>] [-MaxConcurrency <int>] [-MaxEvents <long>] [-MaxEventsScanned <long>] [-ReadMode <EventReadMode>] [-MessageCulture <cultureinfo>] [-SessionTimeoutMs <int>] [-BufferCapacity <int>] [-ExpandData] [-Oldest] [-DisableParallel] [-FallbackMessageCulture <cultureinfo>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-BookmarkXml <string>] [-BookmarkOffset <long>] [-IgnoreStaleBookmark] [-ContinueOnError] [-TolerateQueryErrors] [-IncludeBookmark] [<CommonParameters>]
```

## DESCRIPTION
Enhanced event querying cmdlet that replaces and extends Get-WinEvent functionality.

Supports local and remote logs, built-in event types, custom JSON definitions, record ID resumes, parallel queries, and rich filtering.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXEvent -LogName Security -EventId 4624 -StartTime (Get-Date).AddHours(-1)
```

Shows only successful logons from the last hour.

### EXAMPLE 2
```powershell
Get-EVXEvent -Path C:\Logs\App.evtx -EventId 1000,1001
```

Filters specific application error IDs from an offline log.

### EXAMPLE 3
```powershell
Get-EVXEvent -LogName Security -RecordIdFile C:\temp\resume.json -RecordIdKey Sec
```

Continues from the last processed record and updates the checkpoint file.

### EXAMPLE 4
```powershell
Get-EVXEvent -Type ADUserLogonFailed -StartTime (Get-Date).AddDays(-1)
```

The event type owns its source channel, event IDs, filters, and typed projection.

### EXAMPLE 5
```powershell
$filter = New-EVXFilter -Type ADUserLogonFailed; $filter.AllOf($filter.Fields.Who.MatchesWildcard('EVOTEC\*'), $filter.Fields.IPAddress.MatchesSubnet('10.0.0.0/8')); Get-EVXEvent -Filter $filter -TimePeriod Last7Days
```

The filter retains its type and exact predicate, so the query does not repeat either one.

### EXAMPLE 6
```powershell
Get-EVXEvent -Type ADUserLogonFailed -Describe
```

Returns the source, field, alias, type, and filter-stage metadata without reading events.

### EXAMPLE 7
```powershell
Get-EVXEvent -Definition .\ServiceChanges.json -Path .\System.evtx
```

Applies the definition-owned sources and fields while the path supplies the event container.

### EXAMPLE 8
```powershell
Get-EVXEvent -LogName Security -MachineName DC1,DC2 -EventId 4740 -MaxConcurrency 8
```

Retrieves account lockouts from multiple domain controllers with bounded concurrent source setup.

### EXAMPLE 9
```powershell
Get-EVXEvent -Path C:\Logs\Security.evtx -Oldest -ReadMode Metadata | Select-Object TimeCreated, RecordId, Id, ProviderName, MachineName | Export-Csv C:\Logs\Security-metadata.csv -NoTypeInformation
```

Skips provider message formatting, XML parsing, attachments, and bookmarks while streaming every record.

## PARAMETERS

### -Authentication
Authentication package used for remote Windows Event Log sessions.

```yaml
Type: EventLogAuthentication
Parameter Sets: TypedFilter, Channel, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BookmarkOffset
Record offset relative to BookmarkXml. The default of one resumes after the bookmarked event.

```yaml
Type: Int64
Parameter Sets: Channel, Path, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BookmarkXml
Native bookmark XML used as the seek origin. A bookmark targets one source or one
structured QueryList session and cannot be fanned out across several independent sources.

```yaml
Type: String
Parameter Sets: Channel, Path, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BufferCapacity
Maximum number of projected events buffered between parallel readers and the PowerShell pipeline. Zero selects a bounded default.

```yaml
Type: Int32
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Collector
Windows Event Collector computers from which typed events are read through ForwardedEvents.
The selected Type still owns each event's original source channel and identifiers.

```yaml
Type: List`1
Parameter Sets: TypedFilter, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContinueOnError
Continues other independent sources when a channel, computer, or file query fails.
Each isolated failure is emitted as a non-terminating PowerShell error.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials used for remote channel or structured queries.

```yaml
Type: PSCredential
Parameter Sets: TypedFilter, Channel, Type, Definition, Provider, Hashtable, Xml
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
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Describe
Returns definition and field metadata without querying event sources.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Path, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableParallel
Disables parallel processing of queries.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsMaxConcurrency
Maximum number of reverse-DNS requests that may overlap. Results and checkpoints remain in event order.

```yaml
Type: Int32
Parameter Sets: Type
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsTimeoutMs
Whole-request timeout in milliseconds for each optional reverse-DNS request, including dependency retries.

```yaml
Type: Int32
Parameter Sets: Type
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
End time for the event query.

```yaml
Type: DateTime
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider
Aliases: DateTo
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventId
Event identifiers used to filter results.

```yaml
Type: Int32[]
Parameter Sets: Channel, Path, Provider
Aliases: Id
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventRecordId
Specific event record identifiers to retrieve.

```yaml
Type: Int64[]
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider
Aliases: RecordId
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExpandData
Expands event data into individual properties.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: Expand
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Explain
Returns the native/managed predicate plan without querying event sources.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Path, Type, Definition
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FallbackMessageCulture
Culture used when provider resources do not contain MessageCulture.
Get-EVXEvent requests en-US by default, then falls back to the current UI culture
so deterministic English is preferred without discarding locally available messages.

```yaml
Type: CultureInfo
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
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
Type: Object
Parameter Sets: TypedFilter, Channel, Path, Provider
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilterHashtable
One or more Get-WinEvent compatible hashtables containing LogName, Path, ProviderName, or combinations of them plus event predicates.
Arbitrary keys target named EventData fields. SuppressHashFilter adds native exclusions.

```yaml
Type: Hashtable[]
Parameter Sets: Hashtable
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -FilterXml
A complete Windows Event Log QueryList XML document. This supports multi-channel Select
and Suppress expressions without translating or weakening the supplied query.

```yaml
Type: XmlDocument
Parameter Sets: Xml
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -FilterXPath
A native Windows Event Log XPath expression applied to every LogName or Path.
This cannot be combined with the high-level filter parameters.

```yaml
Type: String
Parameter Sets: Channel, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Includes analytic and debug channels when LogName or ProviderName uses wildcard patterns.
An explicitly named analytic or debug channel never requires Force.

```yaml
Type: SwitchParameter
Parameter Sets: Channel, Provider, Hashtable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IgnoreStaleBookmark
Allows bookmark seek to continue when the exact bookmarked record is not in the result set.

```yaml
Type: SwitchParameter
Parameter Sets: Channel, Path, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeBookmark
Materializes a native bookmark for each returned event. This is disabled by
default because bookmark creation adds native handle and render work per record.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Keywords
Keywords used to filter events.

```yaml
Type: Int64[]
Parameter Sets: Channel, Path, Provider
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Level
Event level (e.g. Error, Warning) used for filtering.

```yaml
Type: Level[]
Parameter Sets: Channel, Path, Provider
Aliases: None
Possible values: LogAlways, Critical, Error, Warning, Informational, Verbose

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Name of the log to query.

```yaml
Type: String[]
Parameter Sets: Channel
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -MachineName
Computer names against which to run the query.

```yaml
Type: List`1
Parameter Sets: TypedFilter, Channel, Type, Definition, Provider, Hashtable, Xml
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrency
Maximum number of independent event sources opened concurrently.

```yaml
Type: Int32
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: NumberOfThreads
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEvents
Maximum number of events to return.

```yaml
Type: Int64
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEventsScanned
Maximum number of merged candidate events delivered for message and checkpoint filtering.
Zero continues until the output limit is satisfied or the query is exhausted. Native selection may perform
one initial lookahead per machine/XPath chunk plus bounded page prefetch; those rows are not evaluated by the cmdlet.

```yaml
Type: Int64
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageCulture
Culture used to format provider messages and display names for offline EVTX queries.
For example, use en-US for deterministic English output.

```yaml
Type: CultureInfo
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageRegex
Filters events by matching their formatted message against the provided regular expression.

```yaml
Type: Regex
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NamedDataExcludeFilter
Hashtable filter to exclude named EventData fields when querying files.

```yaml
Type: Hashtable
Parameter Sets: Channel, Path, Provider, Hashtable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NamedDataFilter
Hashtable filter for named EventData fields when querying files.

```yaml
Type: Hashtable
Parameter Sets: Channel, Path, Provider, Hashtable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Oldest
Reads events from oldest to newest when querying files.

```yaml
Type: SwitchParameter
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to an event log file for offline analysis.

```yaml
Type: String[]
Parameter Sets: Path, Type, Definition
Aliases: PSPath
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -ProviderName
Event provider name to filter results.

```yaml
Type: String[]
Parameter Sets: Path, Provider
Aliases: Source, Provider
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReadMode
Controls per-event materialization. Metadata skips provider messages, XML, attachments, and bookmarks;
Message formats the provider message; StructuredData parses XML without formatting the message;
StructuredDataAndMessage includes both without decoding attachments; Full includes all data.
Typed queries default to StructuredDataAndMessage; other query sets default to Message.

```yaml
Type: EventReadMode
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values: Metadata, Message, StructuredData, RawXml, Full, StructuredDataAndMessage

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecordIdFile
Path to a file storing last processed record ID.

```yaml
Type: String
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecordIdKey
Identifier used when persisting record IDs to allow multiple jobs to share a file.

```yaml
Type: String
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolveDns
Resolves reverse-DNS names for supported typed events after projection. DNS failures remain visible on the
event and never remove the event from the pipeline.

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

### -SessionTimeoutMs
Overrides both remote connection and no-progress read timeouts in milliseconds.
Zero uses Settings.SessionTimeoutMs for connection establishment and
Settings.QuerySessionTimeoutMs for reading.

```yaml
Type: Int32
Parameter Sets: TypedFilter, Channel, Type, Definition, Provider, Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Start time for the event query.

```yaml
Type: DateTime
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider
Aliases: DateFrom
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimePeriod
Relative time period for filtering events.

```yaml
Type: TimePeriod
Parameter Sets: TypedFilter, Channel, Path, Type, Definition, Provider
Aliases: None
Possible values: PastHour, CurrentHour, PastDay, CurrentDay, PastMonth, CurrentMonth, PastQuarter, CurrentQuarter, Last3Days, Last7Days, Last14Days, Last1Hour, Last2Hours, Last3Hours, Last6Hours, Last12Hours, Last16Hours, Last24Hours, Today, Yesterday, Everything, TillLastMonday, TillLastTuesday, TillLastWednesday, TillLastThursday, TillLastFriday, TillLastSaturday, TillLastSunday, Last15Minutes, Last30Minutes

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TolerateQueryErrors
Allows a structured QueryList to continue when one path cannot be evaluated.

```yaml
Type: SwitchParameter
Parameter Sets: Hashtable, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
One or more built-in typed event definitions to query. Each type owns its source channels and event identifiers.

```yaml
Type: EventType[]
Parameter Sets: Type
Aliases: NamedEvent, NamedEvents
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserId
User identifier used to filter events.

```yaml
Type: String[]
Parameter Sets: Channel, Path, Provider
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Where
Reusable typed EventPredicate, predicate JSON, or predicate JSON file.

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

- `System.String[]`
- `System.Collections.Hashtable[]`
- `System.Xml.XmlDocument`

## OUTPUTS

- `EventViewerX.EventObject`
- `EventViewerX.EventTypeRecord`
- `EventViewerX.CustomEventRecord`

## RELATED LINKS

- None
