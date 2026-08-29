---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXAnalysisContract
## SYNOPSIS
Gets versioned EventViewerX analysis JSON contracts.

Returns Draft 2020-12 schemas for observations, findings, coverage, plans, packs, and rule traces.

## SYNTAX
### __AllParameterSets
```powershell
Get-EVXAnalysisContract [[-Kind] <EventAnalysisContractKind[]>] [<CommonParameters>]
```

## DESCRIPTION
Gets versioned EventViewerX analysis JSON contracts.

Returns Draft 2020-12 schemas for observations, findings, coverage, plans, packs, and rule traces.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXAnalysisContract -Kind Finding | Select-Object -ExpandProperty JsonSchema
```

Use the schema to validate downstream JSON integrations.

## PARAMETERS

### -Kind
Optional contract kinds. The default returns every supported contract.

```yaml
Type: EventAnalysisContractKind[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Observation, Finding, Coverage, Plan, Pack, RuleTrace

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

- `EventViewerX.EventAnalysisContractDescriptor`

## RELATED LINKS

- None
