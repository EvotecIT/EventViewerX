---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXDetectionPack
## SYNOPSIS
Gets the built-in versioned EventViewerX detection packs.

Returns signed-content-ready pack manifests with rule provenance, versions, hashes, licenses, and ATT&CK tags.

## SYNTAX
### __AllParameterSets
```powershell
Get-EVXDetectionPack [[-PackId] <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Gets the built-in versioned EventViewerX detection packs.

Returns signed-content-ready pack manifests with rule provenance, versions, hashes, licenses, and ATT&CK tags.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXDetectionPack
```

Returns the native EventViewerX detection packs without loading Storage or Reporting.

### EXAMPLE 2
```powershell
Get-EVXDetectionPack -PackId '*authentication*'
```

Uses PowerShell wildcard matching against stable pack identifiers.

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

- `EventViewerX.EventDetectionPack`

## RELATED LINKS

- None
