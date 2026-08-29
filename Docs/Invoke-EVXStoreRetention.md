---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Invoke-EVXStoreRetention
## SYNOPSIS
Applies explicit EventViewerX event and finding retention.

Prunes source events and durable findings independently and can compact free SQLite pages after deletion.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-EVXStoreRetention [-Path] <string> [-EventRetention <TimeSpan>] [-FindingRetention <TimeSpan>] [-Vacuum] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Applies explicit EventViewerX event and finding retention.

Prunes source events and durable findings independently and can compact free SQLite pages after deletion.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-EVXStoreRetention -Path C:\Data\events.db -EventRetention 30.00:00:00 -FindingRetention 90.00:00:00 -Vacuum
```

Returns deleted row counts and database sizes before and after maintenance.

## PARAMETERS

### -EventRetention
Maximum source-event age.

```yaml
Type: TimeSpan
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FindingRetention
Maximum durable-finding age.

```yaml
Type: TimeSpan
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
EventStore SQLite path.

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

### -Vacuum
Compacts free SQLite pages after rows are removed.

```yaml
Type: SwitchParameter
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

- `EventViewerX.Storage.EventStoreRetentionResult`

## RELATED LINKS

- None
