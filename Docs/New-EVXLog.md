---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# New-EVXLog
## SYNOPSIS
Creates a new Windows event log with optional size and retention settings.

Applies explicit desired state through ClassicEventLogManager and reports exactly what changed.

## SYNTAX
### __AllParameterSets
```powershell
New-EVXLog [-LogName] <string> [[-ProviderName] <string>] [-MachineName <string>] [-MaximumKilobytes <Int64>] [-OverflowAction <OverflowAction>] [-RetentionDays <Int32>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates a new Windows event log with optional size and retention settings.

Applies explicit desired state through ClassicEventLogManager and reports exactly what changed.

## EXAMPLES

### EXAMPLE 1
```powershell
New-EVXLog -LogName MyApp -ProviderName MyApp
```

Creates a new log and provider for application events.

### EXAMPLE 2
```powershell
New-EVXLog -LogName MyApp -MaximumKilobytes 102400 -OverflowAction OverwriteOlder -RetentionDays 30
```

Limits the log to ~100 MB and retains events for 30 days.

### EXAMPLE 3
```powershell
New-EVXLog -LogName MyApp -ProviderName MyApp -MachineName SRV01
```

Creates the log on SRV01.

## PARAMETERS

### -LogName
Name of the log to create.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Target machine on which to create the log.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumKilobytes
Maximum log size in kilobytes.

```yaml
Type: Int64
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OverflowAction
Overflow behavior when the log is full.

```yaml
Type: OverflowAction
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: OverwriteAsNeeded, OverwriteOlder, DoNotOverwrite

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProviderName
Name of the provider associated with the log.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Source, Provider
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetentionDays
Minimum days to retain events when using OverwriteOlder policy.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
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

- `EventViewerX.ClassicEventLogEnsureResult`

## RELATED LINKS

- None
