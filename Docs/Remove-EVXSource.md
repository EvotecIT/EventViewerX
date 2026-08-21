---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Remove-EVXSource
## SYNOPSIS
Removes an event source from Windows Event Log.

Deletes the provider registration locally or on a remote machine with optional log scoping.

## SYNTAX
### __AllParameterSets
```powershell
Remove-EVXSource [-SourceName] <string> [-LogName <string>] [-MachineName <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes an event source from Windows Event Log.

Deletes the provider registration locally or on a remote machine with optional log scoping.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-EVXSource -SourceName MyApp
```

Unregisters the MyApp event source on the local computer.

### EXAMPLE 2
```powershell
Remove-EVXSource -SourceName MyApp -MachineName SRV01
```

Targets the specified remote machine.

### EXAMPLE 3
```powershell
Remove-EVXSource -SourceName MyApp -LogName Application
```

Limits the lookup to the Application log when removing the source.

## PARAMETERS

### -LogName
Optional log name to scope source checks (avoids probing Security/State). Defaults to Application when specified.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Target computer where the source resides.

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

### -SourceName
Name of the event source to remove.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Source, Provider
Possible values:

Required: True
Position: 0
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
