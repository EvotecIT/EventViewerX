---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# Clear-EVXLog
## SYNOPSIS
Clears Windows Event Log channels through the native engine.

Supports local or remote channels, explicit credentials, and an optional native EVTX backup. Failures are terminating and retain their Windows error code.

## SYNTAX
### __AllParameterSets
```powershell
Clear-EVXLog [-LogName] <string[]> [-MachineName <string[]>] [-BackupPath <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-TimeoutMs <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Clears Windows Event Log channels through the native engine.

Supports local or remote channels, explicit credentials, and an optional native EVTX backup. Failures are terminating and retain their Windows error code.

## EXAMPLES

### EXAMPLE 1
```powershell
Clear-EVXLog -LogName Application -BackupPath C:\Backups\Application.evtx
```

Windows writes the backup before clearing the channel.

## PARAMETERS

### -Authentication
Authentication package for the remote session.

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

### -BackupPath
Optional backup EVTX path. This requires exactly one LogName.

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

### -Credential
Credentials for the remote session.

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

### -LogName
Channel names to clear.

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
Remote computer. Omit for local.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: ComputerName, ServerName
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

- `EventViewerX.EventLogClearResult`

## RELATED LINKS

- None
