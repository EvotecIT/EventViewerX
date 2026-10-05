---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Invoke-EVXInvestigation
## SYNOPSIS
Replays a verified investigation without rewriting its original evidence or generated outputs.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-EVXInvestigation [-Session] <EventInvestigationSession> [-AllowDifferentEngine] [<CommonParameters>]
```

## DESCRIPTION
Replays a verified investigation without rewriting its original evidence or generated outputs.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-EVXInvestigation -AllowDifferentEngine
```


## PARAMETERS

### -AllowDifferentEngine
Allows comparative replay with a different engine build and marks resulting coverage incomplete.

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

### -Session
Session returned by Open-EVXInvestigation or Export-EVXInvestigation.

```yaml
Type: EventInvestigationSession
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `EventViewerX.EventInvestigationSession`

## OUTPUTS

- `EventViewerX.EventDetectionExecutionResult`

## RELATED LINKS

- None
