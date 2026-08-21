---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXProvider
## SYNOPSIS
Returns registered provider metadata or EventViewerX provider packages.

The default set supports local and remote provider discovery. Package sets inspect a portable .evxprovider file or list machine-wide EventViewerX-managed installations.

## SYNTAX
### Registered (Default)
```powershell
Get-EVXProvider [[-Name] <string[]>] [-MachineName <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-TimeoutMs <int>] [-Culture <cultureinfo>] [-IncludeEvents] [-NameOnly] [-AsResult] [<CommonParameters>]
```

### Package
```powershell
Get-EVXProvider [-Path] <string> [<CommonParameters>]
```

### InstalledPackage
```powershell
Get-EVXProvider -InstalledPackage [<CommonParameters>]
```

## DESCRIPTION
Returns registered provider metadata or EventViewerX provider packages.

The default set supports local and remote provider discovery. Package sets inspect a portable .evxprovider file or list machine-wide EventViewerX-managed installations.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXProvider -Name '*Security*' | Select-Object Name, LogLinks
```

Returns reusable detached metadata rather than disposable ProviderMetadata handles.

### EXAMPLE 2
```powershell
Get-EVXProvider -Name '*IIS*' -NameOnly
```

Outputs only provider names for scripts that need strings.

### EXAMPLE 3
```powershell
Get-EVXProvider -Path .\Contoso.Scanner-1.0.0.evxprovider
```

Verifies the package and returns its typed schema and trust metadata.

### EXAMPLE 4
```powershell
Get-EVXProvider -InstalledPackage | Select-Object ProviderName, PackageVersion, IsActive, IsRegistered
```

Uses the package inventory parameter set of the same provider catalog command.

## PARAMETERS

### -AsResult
Returns one success/failure result for every matching provider.

```yaml
Type: SwitchParameter
Parameter Sets: Registered
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Authentication
Authentication package for the remote session.

```yaml
Type: EventLogAuthentication
Parameter Sets: Registered
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials for a remote provider catalog.

```yaml
Type: PSCredential
Parameter Sets: Registered
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Culture
Culture used for provider display metadata.

```yaml
Type: CultureInfo
Parameter Sets: Registered
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeEvents
Includes all provider event definitions and templates.

```yaml
Type: SwitchParameter
Parameter Sets: Registered
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InstalledPackage
Lists providers installed through EventViewerX packages.

```yaml
Type: SwitchParameter
Parameter Sets: InstalledPackage
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Remote computer name. Omit for the local computer.

```yaml
Type: String
Parameter Sets: Registered
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Provider names or wildcard patterns.

```yaml
Type: String[]
Parameter Sets: Registered
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NameOnly
Returns provider names instead of metadata snapshots.

```yaml
Type: SwitchParameter
Parameter Sets: Registered
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Portable .evxprovider package to verify and inspect.

```yaml
Type: String
Parameter Sets: Package
Aliases: FullName, OutputPath, PackagePath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -TimeoutMs
Maximum time for remote RPC preflight and session establishment.

```yaml
Type: Int32
Parameter Sets: Registered
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

- `EventViewerX.EventProviderMetadataSnapshot`
- `EventViewerX.EventProviderCatalogResult`
- `EventViewerX.Providers.EventProviderPackage`
- `EventViewerX.Providers.InstalledEventProviderPackage`
- `System.String`

## RELATED LINKS

- None
