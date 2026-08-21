---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXWatcher
## SYNOPSIS
Retrieves information about active EVX watchers.

Filters by watcher Id or Name and returns watcher metadata such as log, machine, filters, and runtime state.

## SYNTAX
### __AllParameterSets
```powershell
Get-EVXWatcher [[-Id] <guid[]>] [-Name <string>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves information about active EVX watchers.

Filters by watcher Id or Name and returns watcher metadata such as log, machine, filters, and runtime state.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXWatcher
```

Shows every currently running watcher.

### EXAMPLE 2
```powershell
Get-EVXWatcher -Name SecurityWatcher
```

Returns only watchers whose name matches SecurityWatcher.

### EXAMPLE 3
```powershell
Get-EVXWatcher -Id 'd9b0e4d1-2d0e-4fa2-9b8f-5b6d2a0ad111'
```

Retrieves a specific watcher instance using its identifier.

## PARAMETERS

### -Id
Identifiers of watchers to retrieve.

```yaml
Type: Guid[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Name
Name of the watcher to return.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Guid[]`

## OUTPUTS

- `EventViewerX.WatcherInfo`

## RELATED LINKS

- None
