---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Test-EVXStore
## SYNOPSIS
Validates an EventViewerX history store.

Runs SQLite integrity checks and verifies the supported base, identity, and finding schema contracts.

## SYNTAX
### __AllParameterSets
```powershell
Test-EVXStore [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Validates an EventViewerX history store.

Runs SQLite integrity checks and verifies the supported base, identity, and finding schema contracts.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EVXStore -Path C:\Data\events.db
```

Returns health, schema versions, row counts, database size, and any diagnostics.

## PARAMETERS

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.Storage.EventStoreIntegrityResult`

## RELATED LINKS

- None
