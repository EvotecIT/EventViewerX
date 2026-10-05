---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Compare-EVXDetectionPack
## SYNOPSIS
Compares detection pack content or previews changes against bounded historical observations.

## SYNTAX
### __AllParameterSets
```powershell
Compare-EVXDetectionPack -Previous <EventDetectionPack> -Current <EventDetectionPack> [-Historical] [-InputObject <EventObservation>] [-MaximumObservations <int>] [-MaximumFindings <int>] [-Options <EventDetectionEngineOptions>] [<CommonParameters>]
```

## DESCRIPTION
Compares detection pack content or previews changes against bounded historical observations.

## EXAMPLES

### EXAMPLE 1
```powershell
Compare-EVXDetectionPack -Previous 'Value' -Current 'Value'
```


## PARAMETERS

### -Current
Proposed version of the pack.

```yaml
Type: EventDetectionPack
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Historical
Evaluates both versions against the same historical sample instead of comparing definitions only.

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

### -InputObject
Canonical historical observation to compare.

```yaml
Type: EventObservation
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaximumFindings
Maximum findings retained from each plan.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumObservations
Maximum historical observations retained for comparison.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Options
Coverage and evaluator bounds shared by both versions.

```yaml
Type: EventDetectionEngineOptions
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Previous
Original version of the pack.

```yaml
Type: EventDetectionPack
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `EventViewerX.EventObservation`

## OUTPUTS

- `EventViewerX.EventDetectionPackComparison`
- `EventViewerX.EventDetectionImpactPreview`

## RELATED LINKS

- None
