---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# Uninstall-EVXProviderPackage
## SYNOPSIS
Unregisters an EventViewerX-managed custom event provider.

Removes the active manifest registration. Package and schema files are retained by default so archived EVTX records remain renderable and the provider can be restored; use RemoveFiles only when that history is no longer required.

## SYNTAX
### __AllParameterSets
```powershell
Uninstall-EVXProviderPackage [-ProviderName] <string> [-RemoveFiles] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Unregisters an EventViewerX-managed custom event provider.

Removes the active manifest registration. Package and schema files are retained by default so archived EVTX records remain renderable and the provider can be restored; use RemoveFiles only when that history is no longer required.

## EXAMPLES

### EXAMPLE 1
```powershell
Uninstall-EVXProviderPackage -ProviderName 'Name'
```


## PARAMETERS

### -ProviderName
Name of an EventViewerX-managed provider.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Name
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -RemoveFiles
Delete retained packages and schemas after unregistering. Old EVTX messages may no longer render on this machine.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `EventViewerX.Providers.EventProviderPackageUninstallResult`

## RELATED LINKS

- None
