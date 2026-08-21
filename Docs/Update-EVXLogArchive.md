---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Update-EVXLogArchive
## SYNOPSIS
Archives provider resources into exported EVTX files.

Makes a Windows-native EVTX export self-contained for message rendering on computers that do not have the source provider installed.

## SYNTAX
### __AllParameterSets
```powershell
Update-EVXLogArchive [-Path] <string[]> [-Culture <cultureinfo>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Archives provider resources into exported EVTX files.

Makes a Windows-native EVTX export self-contained for message rendering on computers that do not have the source provider installed.

## EXAMPLES

### EXAMPLE 1
```powershell
Update-EVXLogArchive -Path C:\Exports\Security.evtx -Culture en-US
```

Updates the exported log in place through EvtArchiveExportedLog.

## PARAMETERS

### -Culture
Provider resource culture. Windows chooses a locale when omitted.

```yaml
Type: CultureInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Exported EVTX files to update.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: FullName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String[]`

## OUTPUTS

- `System.IO.FileInfo`

## RELATED LINKS

- None
