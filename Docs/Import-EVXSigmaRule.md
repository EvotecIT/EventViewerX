---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Import-EVXSigmaRule
## SYNOPSIS
Imports supported Sigma YAML as native EventViewerX detection rules.

The YAML adapter is separate from the detection engine because it adds a YAML dependency. Imported rules execute in the same bounded native engine as built-in rules.

## SYNTAX
### Rule (Default)
```powershell
Import-EVXSigmaRule [-Path] <string[]> [<CommonParameters>]
```

### Pack
```powershell
Import-EVXSigmaRule [-Path] <string[]> -AsPack -PackId <string> -Version <string> [<CommonParameters>]
```

## DESCRIPTION
Imports supported Sigma YAML as native EventViewerX detection rules.

The YAML adapter is separate from the detection engine because it adds a YAML dependency. Imported rules execute in the same bounded native engine as built-in rules.

## EXAMPLES

### EXAMPLE 1
```powershell
$rules = Import-EVXSigmaRule -Path .\rules\*.yml; Get-EVXEvent -LogName Security | Invoke-EVXDetection -Rule $rules
```

Compiles each file once and evaluates it with the shared EventViewerX detection engine.

### EXAMPLE 2
```powershell
Import-EVXSigmaRule -Path .\rule.yml -AsPack -PackId contoso.windows -Version 1.0.0
```

Wraps supported Sigma rules in an integrity-protected native pack.

## PARAMETERS

### -AsPack
Returns one versioned EventViewerX pack instead of individual rules.

```yaml
Type: SwitchParameter
Parameter Sets: Pack
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PackId
Stable pack identifier used with AsPack.

```yaml
Type: String
Parameter Sets: Pack
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
One or more Sigma YAML files to import.

```yaml
Type: String[]
Parameter Sets: Rule, Pack
Aliases: FullName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -Version
Semantic pack version used with AsPack.

```yaml
Type: String
Parameter Sets: Pack
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

- `System.String[]`

## OUTPUTS

- `EventViewerX.IEventDetectionRule`
- `EventViewerX.EventDetectionPack`

## RELATED LINKS

- None
