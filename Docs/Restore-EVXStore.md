---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Restore-EVXStore
## SYNOPSIS
Restores an EventViewerX store from a validated backup.

Validates the backup before replacement, uses an atomic recovery file, and restores the original database automatically when post-replacement validation fails. Stop active readers and writers first.

## SYNTAX
### __AllParameterSets
```powershell
Restore-EVXStore [-Path] <string> [-BackupPath] <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Restores an EventViewerX store from a validated backup.

Validates the backup before replacement, uses an atomic recovery file, and restores the original database automatically when post-replacement validation fails. Stop active readers and writers first.

## EXAMPLES

### EXAMPLE 1
```powershell
Restore-EVXStore -Path C:\Data\events.db -BackupPath C:\Backups\events.db
```

Prompts because the live history database is replaced.

## PARAMETERS

### -BackupPath
Validated backup database path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Live EventStore SQLite path to replace.

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
