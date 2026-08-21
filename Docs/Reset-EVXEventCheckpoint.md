---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# Reset-EVXEventCheckpoint
## SYNOPSIS
Resets persisted event-query checkpoint progress safely.

Starts a new checkpoint generation under the shared file lock so an in-flight query from the previous generation cannot restore stale progress. Use this cmdlet instead of deleting only the RecordIdFile compatibility file because generation state is stored in a visible companion .state.json file.

## SYNTAX
### __AllParameterSets
```powershell
Reset-EVXEventCheckpoint [-Path] <string> [[-Key] <string>] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Resets persisted event-query checkpoint progress safely.

Starts a new checkpoint generation under the shared file lock so an in-flight query from the previous generation cannot restore stale progress. Use this cmdlet instead of deleting only the RecordIdFile compatibility file because generation state is stored in a visible companion .state.json file.

## EXAMPLES

### EXAMPLE 1
```powershell
Reset-EVXEventCheckpoint -Path C:\State\security.json
```

Starts a new generation for every checkpoint key stored in the file.

### EXAMPLE 2
```powershell
Reset-EVXEventCheckpoint -Path C:\State\security.json -Key security-failures -PassThru
```

Resets only the selected key and returns a snapshot containing CheckpointPath, StatePath, and LockPath.

## PARAMETERS

### -Key
Optional checkpoint key. The exact key and its existing per-source derived keys start new generations. When omitted, every existing key starts a new generation.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: RecordIdKey
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Returns the persisted checkpoint snapshot, including companion state and lock paths.

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
Compatibility checkpoint path supplied to Get-EVXEvent as RecordIdFile.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: RecordIdFile
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

- `EventViewerX.EventCheckpointSnapshot`

## RELATED LINKS

- None
