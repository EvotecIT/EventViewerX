---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Set-EVXLog
## SYNOPSIS
Updates Windows Event Log channel policy.

Configures enabled state, maximum size, retention mode, file path, or security descriptor and returns a detailed per-log result.

## SYNTAX
### __AllParameterSets
```powershell
Set-EVXLog [-LogName] <string[]> [-MachineName <string[]>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-TimeoutMs <int>] [-Enabled <Boolean>] [-MaximumSizeMB <Int32>] [-MaximumSizeBytes <Int64>] [-Mode <EventLogMode>] [-LogFilePath <string>] [-SecurityDescriptor <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Updates Windows Event Log channel policy.

Configures enabled state, maximum size, retention mode, file path, or security descriptor and returns a detailed per-log result.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-EVXLog -LogName Security,System -MaximumSizeMB 1024
```

Applies the same policy through the shared EventViewerX channel-policy service.

### EXAMPLE 2
```powershell
Set-EVXLog -LogName 'Microsoft-Windows-TaskScheduler/Operational' -Enabled $true
```

Returns which properties were applied, skipped, or failed.

## PARAMETERS

### -Authentication
Authentication package for remote channel-policy sessions.

```yaml
Type: EventLogAuthentication
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Default, Negotiate, Kerberos, Ntlm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials for remote channel-policy sessions.

```yaml
Type: PSCredential
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Enabled
Enables or disables the channel.

```yaml
Type: Boolean
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogFilePath
Backing log file path.

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
Channel names to update.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -MachineName
Target computers. Omit for the local computer.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -MaximumSizeBytes
Maximum channel size in bytes.

```yaml
Type: Int64
Parameter Sets: __AllParameterSets
Aliases: MaximumSizeInBytes
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumSizeMB
Maximum channel size in megabytes.

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

### -Mode
Circular, AutoBackup, or Retain channel mode.

```yaml
Type: EventLogMode
Parameter Sets: __AllParameterSets
Aliases: LogMode
Possible values: Circular, AutoBackup, Retain

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecurityDescriptor
Channel access-control descriptor in SDDL form.

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

### -TimeoutMs
Maximum time for remote RPC preflight and session establishment.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String[]`

## OUTPUTS

- `EventViewerX.ChannelPolicyApplyResult`

## RELATED LINKS

- None
