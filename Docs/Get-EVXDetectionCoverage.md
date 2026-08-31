---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXDetectionCoverage
## SYNOPSIS
Gets evidence and readiness requirements for built-in EventViewerX detection packs.

Returns channels, providers, event IDs, typed projections, audit policies, target roles, and configuration prerequisites.

## SYNTAX
### __AllParameterSets
```powershell
Get-EVXDetectionCoverage [[-PackId] <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Gets evidence and readiness requirements for built-in EventViewerX detection packs.

Returns channels, providers, event IDs, typed projections, audit policies, target roles, and configuration prerequisites.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXDetectionCoverage -PackId '*authentication*'
```

Use the result before interpreting an empty detection run as a clean environment.

## PARAMETERS

### -PackId
Optional stable pack identifier wildcard.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
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

- `EventViewerX.EventDetectionPackCoverage`

## RELATED LINKS

- None
