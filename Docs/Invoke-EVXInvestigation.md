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
### Events (Default)
```powershell
Invoke-EVXInvestigation [-Session] <EventInvestigationSession> [-AllowDifferentEngine] [<CommonParameters>]
```

### Endpoint
```powershell
Invoke-EVXInvestigation [-Session] <EventInvestigationSession> -Endpoint [-AllowDifferentEngine] [-HtmlPath <string>] [-IncludeSensitiveEvidence] [<CommonParameters>]
```

## DESCRIPTION
Replays a verified investigation without rewriting its original evidence or generated outputs.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-EVXInvestigation -Session 'Value'
```


### EXAMPLE 2
```powershell
Invoke-EVXInvestigation -Session 'Value' -Endpoint
```


## PARAMETERS

### -AllowDifferentEngine
Allows comparative replay with a different engine build and marks resulting coverage incomplete.

```yaml
Type: SwitchParameter
Parameter Sets: Events, Endpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Endpoint
Replays captured endpoint evidence. The default replays native Windows-event detection.

```yaml
Type: SwitchParameter
Parameter Sets: Endpoint
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HtmlPath
New HTML presentation export outside the immutable session directory. Existing files are rejected.

```yaml
Type: String
Parameter Sets: Endpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeSensitiveEvidence
Includes raw messages and DSRegCmd fields in the presentation export. The report remains sensitive even when raw evidence is omitted.

```yaml
Type: SwitchParameter
Parameter Sets: Endpoint
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
Parameter Sets: Events, Endpoint
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
- `EventViewerX.EventEndpointAnalysis`

## RELATED LINKS

- None
