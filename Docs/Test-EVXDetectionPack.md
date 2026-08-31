---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Test-EVXDetectionPack
## SYNOPSIS
Runs the executable fixture contracts shipped with built-in EventViewerX detection packs.

Runs positive, negative, exact-boundary, and known-benign scenarios against each rule in isolation.

## SYNTAX
### __AllParameterSets
```powershell
Test-EVXDetectionPack [[-RuleId] <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Runs the executable fixture contracts shipped with built-in EventViewerX detection packs.

Runs positive, negative, exact-boundary, and known-benign scenarios against each rule in isolation.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EVXDetectionPack
```

Every returned result should have IsMatch set to true.

## PARAMETERS

### -RuleId
Optional stable rule identifier wildcard.

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

- `EventViewerX.EventDetectionFixtureResult`

## RELATED LINKS

- None
