---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Write-EVXEvent
## SYNOPSIS
Writes classic Event Log entries or registered manifest/ETW events.

The Classic parameter set writes through a registered classic source. Manifest parameter sets resolve the registered event schema, validate native values, and write positional, named, or typed payloads.

## SYNTAX
### ByIdPayload (Default)
```powershell
Write-EVXEvent [-ProviderName] <string> [-Id] <int> [[-Payload] <Object[]>] [-Version <Byte>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByIdData
```powershell
Write-EVXEvent [-ProviderName] <string> [-Id] <int> [-Data] <IDictionary> [-Version <Byte>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByNameData
```powershell
Write-EVXEvent [-ProviderName] <string> [-EventName] <string> [-Data] <IDictionary> [-Version <Byte>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Classic
```powershell
Write-EVXEvent [-LogName] <string> [-ProviderName] <string> [-Id] <int> [-Message] <string> [-MachineName <string>] [-EventLogEntryType <EventLogEntryType>] [-Category <int>] [-AdditionalFields <string[]>] [-CreateSource] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Writes classic Event Log entries or registered manifest/ETW events.

The Classic parameter set writes through a registered classic source. Manifest parameter sets resolve the registered event schema, validate native values, and write positional, named, or typed payloads.

## EXAMPLES

### EXAMPLE 1
```powershell
PS> Write-EVXEvent -ProviderName Contoso.Scanner -EventName ScanCompleted -Data @{ FindingCount = 7; ComputerName = $env:COMPUTERNAME }
```

Maps values to the manifest's canonical order by field name and writes the event.

### EXAMPLE 2
```powershell
PS> Write-EVXEvent -ProviderName Microsoft-Windows-PowerShell -Id 45090 -Payload Workflow, Running
```

Uses the positional compatibility surface for an existing Windows provider.

## PARAMETERS

### -AdditionalFields
Additional replacement strings stored with the classic entry.

```yaml
Type: String[]
Parameter Sets: Classic
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Category
Classic event category.

```yaml
Type: Int32
Parameter Sets: Classic
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CreateSource
Registers a missing classic source before writing. Registration normally requires elevation.

```yaml
Type: SwitchParameter
Parameter Sets: Classic
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Data
Hashtable of values keyed by manifest field name. Key order is ignored. Accepts pipeline input for efficient repeated writes with one cached native registration.

```yaml
Type: IDictionary
Parameter Sets: ByIdData, ByNameData
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -EventLogEntryType
Classic event entry severity.

```yaml
Type: EventLogEntryType
Parameter Sets: Classic
Aliases: EntryType
Possible values: Error, Warning, Information, SuccessAudit, FailureAudit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventName
Friendly event name from an EventViewerX-managed provider package.

```yaml
Type: String
Parameter Sets: ByNameData
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Id
Event identifier declared by the provider manifest.

```yaml
Type: Int32
Parameter Sets: ByIdPayload, ByIdData, Classic
Aliases: EventId
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Classic event log receiving the entry.

```yaml
Type: String
Parameter Sets: Classic
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Remote computer receiving the classic entry. Omit for the local computer.

```yaml
Type: String
Parameter Sets: Classic
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Message
Message written to the classic event source.

```yaml
Type: String
Parameter Sets: Classic
Aliases: None
Possible values:

Required: True
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Payload
Ordered values for the compatibility surface. Prefer Data for custom providers.

```yaml
Type: Object[]
Parameter Sets: ByIdPayload
Aliases: None
Possible values:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProviderName
Name of a registered local manifest event provider.

```yaml
Type: String
Parameter Sets: ByIdPayload, ByIdData, ByNameData, Classic
Aliases: Source, Provider
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Version
Event version. Required when the selected identity has multiple schema versions.

```yaml
Type: Byte
Parameter Sets: ByIdPayload, ByIdData, ByNameData
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

- `System.Collections.IDictionary`

## OUTPUTS

- `EventViewerX.ManifestEventWriteResult`

## RELATED LINKS

- None
