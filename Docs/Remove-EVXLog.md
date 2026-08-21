---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Remove-EVXLog
## SYNOPSIS
Removes an event log from the system.

Supports local or remote removal with ShouldProcess confirmation; useful for cleanup of custom logs.

## SYNTAX
### __AllParameterSets
```powershell
Remove-EVXLog [-LogName] <string> [-MachineName <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes an event log from the system.

Supports local or remote removal with ShouldProcess confirmation; useful for cleanup of custom logs.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-EVXLog -LogName MyApp
```

Deletes the MyApp log from the local computer.

### EXAMPLE 2
```powershell
Remove-EVXLog -LogName MyApp -MachineName SRV01
```

Deletes the log on SRV01.

### EXAMPLE 3
```powershell
Remove-EVXLog -LogName MyApp -Confirm
```

Asks for confirmation prior to deletion.

## PARAMETERS

### -LogName
Name of the log to remove.

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
Target machine from which to remove the log.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `System.Boolean`

## RELATED LINKS

- None
