---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Export-EVXInvestigation
## SYNOPSIS
Captures a reproducible investigation with copied input evidence, canonical observations, effective rules, and verified outputs.

## SYNTAX
### Events (Default)
```powershell
Export-EVXInvestigation [-Path] <string> -InputObject <Object> -Manifest <EventInvestigationManifest> -Plan <EventDetectionPlan> [-InputPath <string[]>] [-Coverage <EventDetectionCoverage>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Endpoint
```powershell
Export-EVXInvestigation [-Path] <string> -EndpointCapture <EventEndpointCapture> [-InputObject <Object>] [-Manifest <EventInvestigationManifest>] [-Plan <EventDetectionPlan>] [-Coverage <EventDetectionCoverage>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Use a new directory. The manifest declares the full input window, including correlation warm-up. Existing evidence is never overwritten.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-EVXInvestigation -Path 'C:\Path' -InputObject 'Value' -Manifest 'Value' -Plan 'Value'
```


### EXAMPLE 2
```powershell
Export-EVXInvestigation -Path 'C:\Path' -EndpointCapture 'Value'
```


## PARAMETERS

### -Coverage
Expected and observed collection coverage.

```yaml
Type: EventDetectionCoverage
Parameter Sets: Events, Endpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndpointCapture
Endpoint capture descriptors, intended join state, and finite evidence bounds. Originals are copied before parsing; commands are never executed.

```yaml
Type: EventEndpointCapture
Parameter Sets: Endpoint
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Canonical observation or detached event to retain.

```yaml
Type: Object
Parameter Sets: Events, Endpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -InputPath
Original input files to copy and hash inside the session.

```yaml
Type: String[]
Parameter Sets: Events
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Manifest
Source receipts, query identity, time range, parser versions, and execution limits.

```yaml
Type: EventInvestigationManifest
Parameter Sets: Events, Endpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
New investigation directory.

```yaml
Type: String
Parameter Sets: Events, Endpoint
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Plan
Effective detection plan to retain and evaluate.

```yaml
Type: EventDetectionPlan
Parameter Sets: Events, Endpoint
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

- `System.Object`

## OUTPUTS

- `EventViewerX.EventInvestigationSession`

## RELATED LINKS

- None
