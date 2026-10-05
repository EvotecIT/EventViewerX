---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Open-EVXInvestigation
## SYNOPSIS
Opens an investigation after verifying every retained input and output artifact.

## SYNTAX
### __AllParameterSets
```powershell
Open-EVXInvestigation [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Opens an investigation after verifying every retained input and output artifact.

## EXAMPLES

### EXAMPLE 1
```powershell
Open-EVXInvestigation -Path 'C:\Path'
```


## PARAMETERS

### -Path
Directory containing manifest.json and its retained evidence.

```yaml
Type: String
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

- `System.String`

## OUTPUTS

- `EventViewerX.EventInvestigationSession`

## RELATED LINKS

- None
