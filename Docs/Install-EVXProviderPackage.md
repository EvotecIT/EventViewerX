---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Install-EVXProviderPackage
## SYNOPSIS
Installs or upgrades a portable custom Windows event provider package.

Verifies package hashes and signatures before changing machine state, enforces schema and version compatibility, stages resources under ProgramData, registers the manifest, verifies Windows metadata and channels, and rolls back to the previous provider if activation fails.

The target machine does not require the Windows SDK, Visual Studio, a C# compiler, generated source, or package build tools.

## SYNTAX
### __AllParameterSets
```powershell
Install-EVXProviderPackage [-Path] <string> [-TrustMode <EventProviderPackageTrustMode>] [-TrustedSignerThumbprint <string[]>] [-AllowDowngrade] [-AllowSameVersionReplacement] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Installs or upgrades a portable custom Windows event provider package.

Verifies package hashes and signatures before changing machine state, enforces schema and version compatibility, stages resources under ProgramData, registers the manifest, verifies Windows metadata and channels, and rolls back to the previous provider if activation fails.

The target machine does not require the Windows SDK, Visual Studio, a C# compiler, generated source, or package build tools.

## EXAMPLES

### EXAMPLE 1
```powershell
Install-EVXProviderPackage -Path 'C:\Path'
```


## PARAMETERS

### -AllowDowngrade
Allow a compatible lower package version to replace the active version.

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

### -AllowSameVersionReplacement
Allow different package bytes to reuse the active version. Prefer publishing a new immutable version.

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

### -Path
Portable .evxprovider package path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: FullName, OutputPath, PackagePath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -TrustedSignerThumbprint
Optional exact signer-thumbprint allowlist for RequireTrustedSignature. When supplied, certificates that do not match a pin are rejected even when Windows trusts their chain.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TrustMode
Package trust policy. RequireTrustedSignature requires an exact configured signer thumbprint when pins are supplied; otherwise it requires a Windows-trusted certificate with the Code Signing EKU.

```yaml
Type: EventProviderPackageTrustMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: AllowUnsigned, RequireSignature, RequireTrustedSignature

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

- `EventViewerX.Providers.EventProviderPackageInstallResult`

## RELATED LINKS

- None
