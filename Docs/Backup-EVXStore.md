---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Backup-EVXStore
## SYNOPSIS
Creates a consistent validated EventViewerX store backup.

Uses SQLite snapshot semantics, validates the generated database, and returns its size and SHA-256 checksum.

## SYNTAX
### __AllParameterSets
```powershell
Backup-EVXStore [-Path] <string> [-Destination] <string> [-Force] [<CommonParameters>]
```

## DESCRIPTION
Creates a consistent validated EventViewerX store backup.

Uses SQLite snapshot semantics, validates the generated database, and returns its size and SHA-256 checksum.

## EXAMPLES

### EXAMPLE 1
```powershell
Backup-EVXStore -Path C:\Data\events.db -Destination C:\Backups\events.db
```

Fails if the destination exists unless Force is supplied.

## PARAMETERS

### -Destination
Destination backup database path.

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

### -Force
Atomically replaces an existing destination backup.

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

### -Path
Live EventStore SQLite path.

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

- `EventViewerX.Storage.EventStoreBackupResult`

## RELATED LINKS

- None
