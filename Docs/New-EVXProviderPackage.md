---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# New-EVXProviderPackage
## SYNOPSIS
Compiles a portable custom Windows event provider package.

Validates the schema, optionally compares a compatibility baseline, compiles the Windows event metadata and localized messages in-process, hashes every file, optionally signs package identity and hashes, and emits one portable .evxprovider file.

No Windows SDK, Visual Studio, native compiler, generated source, or external build tool is required.

## SYNTAX
### Definition
```powershell
New-EVXProviderPackage [-Definition] <Object> [-OutputPath] <string> [-BaselinePath <string>] [-SigningCertificate <X509Certificate2>] [-CertificateThumbprint <string>] [-Force] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### DefinitionPath
```powershell
New-EVXProviderPackage [-DefinitionPath] <string> [-OutputPath] <string> [-BaselinePath <string>] [-SigningCertificate <X509Certificate2>] [-CertificateThumbprint <string>] [-Force] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Compiles a portable custom Windows event provider package.

Validates the schema, optionally compares a compatibility baseline, compiles the Windows event metadata and localized messages in-process, hashes every file, optionally signs package identity and hashes, and emits one portable .evxprovider file.

No Windows SDK, Visual Studio, native compiler, generated source, or external build tool is required.

## EXAMPLES

### EXAMPLE 1
```powershell
New-EVXProviderPackage -BaselinePath 'C:\Path'
```


## PARAMETERS

### -BaselinePath
Earlier .evxprovider package or definition JSON used to prevent breaking schema changes.

```yaml
Type: String
Parameter Sets: Definition, DefinitionPath
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificateThumbprint
Thumbprint resolved from CurrentUser\My or LocalMachine\My for package signing.

```yaml
Type: String
Parameter Sets: Definition, DefinitionPath
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Definition
Typed definition or friendly PowerShell hashtable.

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

### -DefinitionPath
UTF-8 provider definition JSON file.

```yaml
Type: String
Parameter Sets: DefinitionPath
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Replace an existing output package.

```yaml
Type: SwitchParameter
Parameter Sets: Definition, DefinitionPath
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Destination .evxprovider package path.

```yaml
Type: String
Parameter Sets: Definition, DefinitionPath
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SigningCertificate
RSA certificate with a private key used to sign package identity and file hashes.

```yaml
Type: X509Certificate2
Parameter Sets: Definition, DefinitionPath
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

- `System.Object`

## OUTPUTS

- `EventViewerX.Providers.EventProviderPackageBuildResult`

## RELATED LINKS

- None
