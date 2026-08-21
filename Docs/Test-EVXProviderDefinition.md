---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Test-EVXProviderDefinition
## SYNOPSIS
Validates a custom Windows event provider definition.

Checks provider identity, channels, event versions, field references, maps, localization, Windows limits, and schema compatibility before any native build tools or machine registration are used.

## SYNTAX
### Definition
```powershell
Test-EVXProviderDefinition [-Definition] <Object> [<CommonParameters>]
```

### Path
```powershell
Test-EVXProviderDefinition [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Validates a custom Windows event provider definition.

Checks provider identity, channels, event versions, field references, maps, localization, Windows limits, and schema compatibility before any native build tools or machine registration are used.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EVXProviderDefinition -Path 'C:\Path'
```


## PARAMETERS

### -Definition
Definition object or friendly PowerShell hashtable.

```yaml
Type: Object
Parameter Sets: Definition
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
UTF-8 provider definition JSON file.

```yaml
Type: String
Parameter Sets: Path
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `EventViewerX.Providers.EventProviderValidationResult`

## RELATED LINKS

- None
