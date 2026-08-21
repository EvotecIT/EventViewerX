---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# New-EVXSource
## SYNOPSIS
Registers a classic Windows Event Log source explicitly.

Creates only the requested source registration and supports provider message, parameter, and category resource files. The command reports whether it created anything.

## SYNTAX
### __AllParameterSets
```powershell
New-EVXSource [-SourceName] <string> [-LogName] <string> [-MachineName <string>] [-MessageResourceFile <string>] [-ParameterResourceFile <string>] [-CategoryResourceFile <string>] [-CategoryCount <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Registers a classic Windows Event Log source explicitly.

Creates only the requested source registration and supports provider message, parameter, and category resource files. The command reports whether it created anything.

## EXAMPLES

### EXAMPLE 1
```powershell
New-EVXSource -SourceName MyApp -LogName Application
```

Registers MyApp explicitly so later Write-EVXEvent calls do not need administrative configuration behavior.

## PARAMETERS

### -CategoryCount
Number of categories in CategoryResourceFile.

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

### -CategoryResourceFile
Optional provider category resource DLL.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Classic log that owns the source.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Optional remote target.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageResourceFile
Optional provider message resource DLL.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ParameterResourceFile
Optional provider parameter resource DLL.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceName
Source name to register.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Source, Provider, ProviderName
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

- `None`

## OUTPUTS

- `System.Boolean`

## RELATED LINKS

- None
