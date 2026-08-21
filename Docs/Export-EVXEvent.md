---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Export-EVXEvent
## SYNOPSIS
Streams Windows events directly to CSV, JSON Lines, XML, or native EVTX.

Uses the EventViewerX native engine and writes directly to the destination without materializing PowerShell objects. Completed output is promoted atomically, so cancellation or failure does not replace an existing file.

## SYNTAX
### Path (Default)
```powershell
Export-EVXEvent [-Path] <string[]> [-OutputPath] <string> [-Format <EventExportFormat>] [-ReadMode <EventReadMode>] [-FilterXPath <string>] [-Filter <EventFilter>] [-EventId <int[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-Oldest] [-MaxEvents <long>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-Force] [-SkipHash] [-ArchiveResources] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Channel
```powershell
Export-EVXEvent [-LogName] <string[]> [-OutputPath] <string> [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Format <EventExportFormat>] [-ReadMode <EventReadMode>] [-FilterXPath <string>] [-Filter <EventFilter>] [-EventId <int[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-Oldest] [-MaxEvents <long>] [-RemoteConnectionTimeoutMilliseconds <int>] [-RemoteReadTimeoutMilliseconds <int>] [-BufferCapacity <int>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-Force] [-SkipHash] [-ArchiveResources] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Provider
```powershell
Export-EVXEvent [-ProviderName] <string[]> [-OutputPath] <string> [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Format <EventExportFormat>] [-ReadMode <EventReadMode>] [-Filter <EventFilter>] [-EventId <int[]>] [-Level <Level[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-Oldest] [-MaxEvents <long>] [-RemoteConnectionTimeoutMilliseconds <int>] [-RemoteReadTimeoutMilliseconds <int>] [-BufferCapacity <int>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-Force] [-SkipHash] [-ArchiveResources] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Xml
```powershell
Export-EVXEvent [-FilterXml] <string> [-OutputPath] <string> [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-Format <EventExportFormat>] [-ReadMode <EventReadMode>] [-Oldest] [-MaxEvents <long>] [-RemoteConnectionTimeoutMilliseconds <int>] [-RemoteReadTimeoutMilliseconds <int>] [-BufferCapacity <int>] [-MessageCulture <cultureinfo>] [-FallbackMessageCulture <cultureinfo>] [-Force] [-SkipHash] [-ArchiveResources] [-TolerateQueryErrors] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Streams Windows events directly to CSV, JSON Lines, XML, or native EVTX.

Uses the EventViewerX native engine and writes directly to the destination without materializing PowerShell objects. Completed output is promoted atomically, so cancellation or failure does not replace an existing file.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-EVXEvent -Path C:\Logs\Security.evtx -OutputPath C:\Exports\Security.jsonl -Format JsonLines -MessageCulture en-US
```

Streams complete projected events directly to one JSON object per line.

### EXAMPLE 2
```powershell
Export-EVXEvent -Path C:\Logs\System.evtx -OutputPath C:\Exports\System.csv -Format Csv -ReadMode Metadata
```

Skips provider messages, XML, and payload parsing while writing a stable CSV schema.

### EXAMPLE 3
```powershell
Export-EVXEvent -Path C:\Logs\Application.evtx -OutputPath C:\Exports\Errors.xml -Format Xml -XPath "*[System[Level=2]]"
```

Writes matching native event XML fragments inside one well-formed Events document.

### EXAMPLE 4
```powershell
Export-EVXEvent -LogName Security -MachineName DC1 -OutputPath C:\Exports\DC1-Security.jsonl -Format JsonLines -ReadMode Full -MessageCulture en-US
```

Uses the bounded native remote reader and avoids a PowerShell object-to-file pipeline.

## PARAMETERS

### -ArchiveResources
Embeds provider resources into a native EVTX export so messages can be rendered
on computers where the original providers are not installed.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Authentication
Authentication package for a remote channel export.

```yaml
Type: EventLogAuthentication
Parameter Sets: Channel, Provider, Xml
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BufferCapacity
Maximum number of detached remote events buffered between the native reader and exporter.

```yaml
Type: Int32
Parameter Sets: Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials for a remote channel export.

```yaml
Type: PSCredential
Parameter Sets: Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Absolute end of the event time range.

```yaml
Type: DateTime
Parameter Sets: Path, Channel, Provider
Aliases: DateTo
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventId
Event identifiers selected natively.

```yaml
Type: Int32[]
Parameter Sets: Path, Channel, Provider
Aliases: Id
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FallbackMessageCulture
Culture used when provider resources do not contain MessageCulture.

```yaml
Type: CultureInfo
Parameter Sets: Path, Channel, Provider, Xml
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
Parameter Sets: Path, Channel, Provider
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilterXml
Complete QueryList XML for a direct multi-channel or multi-file export.

```yaml
Type: String
Parameter Sets: Xml
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilterXPath
Native Windows event XPath expression. The default selects every record.

```yaml
Type: String
Parameter Sets: Path, Channel
Aliases: XPath
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Replaces an existing destination only after the new export completes successfully.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format
Direct streaming format written by the native engine.

```yaml
Type: EventExportFormat
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values: Csv, JsonLines, Xml, Evtx

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Level
Event levels selected natively.

```yaml
Type: Level[]
Parameter Sets: Path, Channel, Provider
Aliases: None
Possible values: LogAlways, Critical, Error, Warning, Informational, Verbose

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Local or remote Windows event channel name.

```yaml
Type: String[]
Parameter Sets: Channel
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Remote computer name. Omit to export the local channel.

```yaml
Type: String
Parameter Sets: Channel, Provider, Xml
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEvents
Maximum number of records written. Zero writes every match.

```yaml
Type: Int64
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageCulture
Culture used for provider messages and display names, for example en-US.

```yaml
Type: CultureInfo
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Oldest
Returns records from oldest to newest.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Destination path. The parent directory must already exist.

```yaml
Type: String
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to an offline log accepted by the Windows Event Log API. EVTX is the validated format.

```yaml
Type: String[]
Parameter Sets: Path
Aliases: LiteralPath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProviderName
Registered provider names or wildcard patterns.

```yaml
Type: String[]
Parameter Sets: Provider
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReadMode
Amount of event data projected into CSV or JSON Lines records.
XML always streams the raw native event XML and ignores this value.

```yaml
Type: EventReadMode
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values: Metadata, Message, StructuredData, RawXml, Full, StructuredDataAndMessage

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RemoteConnectionTimeoutMilliseconds
Maximum time for remote RPC probing, worker admission, and session establishment.

```yaml
Type: Int32
Parameter Sets: Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RemoteReadTimeoutMilliseconds
Maximum time without remote read progress. Zero keeps the read unbounded.

```yaml
Type: Int32
Parameter Sets: Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipHash
Skips the final SHA-256 pass. Use this for maximum throughput when another system
already provides integrity validation.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Channel, Provider, Xml
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Absolute beginning of the event time range.

```yaml
Type: DateTime
Parameter Sets: Path, Channel, Provider
Aliases: DateFrom
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimePeriod
Named relative time range, such as LastHour or CurrentDay.

```yaml
Type: TimePeriod
Parameter Sets: Path, Channel, Provider
Aliases: None
Possible values: PastHour, CurrentHour, PastDay, CurrentDay, PastMonth, CurrentMonth, PastQuarter, CurrentQuarter, Last3Days, Last7Days, Last14Days, Last1Hour, Last2Hours, Last3Hours, Last6Hours, Last12Hours, Last16Hours, Last24Hours, Today, Yesterday, Everything, TillLastMonday, TillLastTuesday, TillLastWednesday, TillLastThursday, TillLastFriday, TillLastSaturday, TillLastSunday, Last15Minutes, Last30Minutes

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TolerateQueryErrors
Allows a structured QueryList export to continue when one path cannot be evaluated.

```yaml
Type: SwitchParameter
Parameter Sets: Xml
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

- `EventViewerX.EventExportResult`

## RELATED LINKS

- None
