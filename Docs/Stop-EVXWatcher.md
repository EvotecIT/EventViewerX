---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# Stop-EVXWatcher
## SYNOPSIS
Stops running EVX watchers by identifier, name, or en masse.

Requires exactly one selector and reports missing identifiers or names instead of silently doing nothing. Use PassThru to return each watcher that was stopped.

## SYNTAX
### ById (Default)
```powershell
Stop-EVXWatcher [-Id] <guid[]> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByName
```powershell
Stop-EVXWatcher -Name <string> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### All
```powershell
Stop-EVXWatcher -All [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Stops running EVX watchers by identifier, name, or en masse.

Requires exactly one selector and reports missing identifiers or names instead of silently doing nothing. Use PassThru to return each watcher that was stopped.

## EXAMPLES

### EXAMPLE 1
```powershell
Stop-EVXWatcher -Id 7b4b6d2c-6c2e-47e1-9c3a-1b5a0a4b9d11
```

Stops the watcher with the specified identifier.

### EXAMPLE 2
```powershell
Stop-EVXWatcher -Name SecurityWatcher -PassThru
```

Stops and returns all watchers whose name matches SecurityWatcher.

### EXAMPLE 3
```powershell
Stop-EVXWatcher -All -Confirm:$false
```

Stops every running watcher after ShouldProcess confirmation.

## PARAMETERS

### -All
Stops all running watchers.

```yaml
Type: SwitchParameter
Parameter Sets: All
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Id
Identifiers of watchers to stop.

```yaml
Type: Guid[]
Parameter Sets: ById
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Name
Name of the watchers to stop.

```yaml
Type: String
Parameter Sets: ByName
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Returns each watcher that was stopped.

```yaml
Type: SwitchParameter
Parameter Sets: ById, ByName, All
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

- `System.Guid[]`

## OUTPUTS

- `EventViewerX.WatcherInfo`

## RELATED LINKS

- None
