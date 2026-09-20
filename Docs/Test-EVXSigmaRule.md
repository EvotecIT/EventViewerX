---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Test-EVXSigmaRule
## SYNOPSIS
Validates and compiles Sigma YAML against the EventViewerX supported subset.

Returns structured diagnostics and native rules without executing them. Unsupported behavior is reported explicitly and is never weakened silently.

## SYNTAX
### __AllParameterSets
```powershell
Test-EVXSigmaRule [-Path] <string[]> [-TelemetryProfile <string>] [<CommonParameters>]
```

## DESCRIPTION
Validates and compiles Sigma YAML against the EventViewerX supported subset.

Returns structured diagnostics and native rules without executing them. Unsupported behavior is reported explicitly and is never weakened silently.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EVXSigmaRule -Path .\rules\suspicious-logon.yml
```

Returns the compiled rules, diagnostics, and IsSupported status.

## PARAMETERS

### -Path
One or more Sigma YAML files to validate.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: FullName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -TelemetryProfile
Explicit telemetry assumptions used for category-only Sigma log sources.
Strict is lossless and rejects categories without exact native selectors.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Strict, WindowsSysmonAndPowerShell

Required: False
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

- `EventViewerX.Sigma.SigmaCompilationResult`

## RELATED LINKS

- None
