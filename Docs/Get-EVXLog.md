---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXLog
## SYNOPSIS
Retrieves event log details by name.

Lists log metadata (size, record count, status) on local or remote machines; supports wildcards.

## SYNTAX
### Channel (Default)
```powershell
Get-EVXLog [-LogName] <string[]> [-MachineName <string[]>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-AsResult] [-TimeoutMs <int>] [-IncludeEventTimes] [-Force] [<CommonParameters>]
```

### Path
```powershell
Get-EVXLog [-Path] <string[]> [<CommonParameters>]
```

## DESCRIPTION
Retrieves event log details by name.

Lists log metadata (size, record count, status) on local or remote machines; supports wildcards.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXLog -LogName Security
```

Shows details for the Security log on the local computer.

### EXAMPLE 2
```powershell
Get-EVXLog -LogName Application,System -MachineName SRV01
```

Retrieves Application and System log info from SRV01.

### EXAMPLE 3
```powershell
Get-EVXLog -LogName "Microsoft-Windows-*"
```

Lists all Microsoft-Windows prefixed logs.

## PARAMETERS

### -AsResult
Returns typed diagnostic results for successful, inaccessible, missing, and partially readable logs.

```yaml
Type: SwitchParameter
Parameter Sets: Channel
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Authentication
Authentication package for remote channel enumeration.

```yaml
Type: EventLogAuthentication
Parameter Sets: Channel
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials for remote channel enumeration.

```yaml
Type: PSCredential
Parameter Sets: Channel
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Includes analytic and debug channels when LogName contains wildcards.

```yaml
Type: SwitchParameter
Parameter Sets: Channel
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeEventTimes
Reads the oldest and newest event timestamps using the same session. This adds two indexed reads per log.

```yaml
Type: SwitchParameter
Parameter Sets: Channel
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogName
Name of the log to retrieve. Wildcards supported.

```yaml
Type: String[]
Parameter Sets: Channel
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Target machines to query.

```yaml
Type: String[]
Parameter Sets: Channel
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Offline EVTX files whose native archive metadata should be read.

```yaml
Type: String[]
Parameter Sets: Path
Aliases: FilePath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutMs
Session-open timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: Channel
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

- `None`

## OUTPUTS

- `EventViewerX.EventLogDetails`
- `EventViewerX.EventLogDetailsResult`
- `EventViewerX.EventLogFileInformation`

## RELATED LINKS

- None
