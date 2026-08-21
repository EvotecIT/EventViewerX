---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Test-EVXLog
## SYNOPSIS
Runs a bounded Windows Event Log connectivity and query probe.

Executes a metadata-only query through the same owned native reader used by Get-EVXEvent, within a fixed budget, and returns the newest matching timestamp plus optional channel metadata.

## SYNTAX
### __AllParameterSets
```powershell
Test-EVXLog [-LogName] <string[]> [-MachineName <string[]>] [-XPath <string>] [-Credential <pscredential>] [-Authentication <EventLogAuthentication>] [-TimeoutMs <int>] [-MaxEventsToScan <int>] [<CommonParameters>]
```

## DESCRIPTION
Runs a bounded Windows Event Log connectivity and query probe.

Executes a metadata-only query through the same owned native reader used by Get-EVXEvent, within a fixed budget, and returns the newest matching timestamp plus optional channel metadata.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EVXLog -LogName System
```

Returns a typed status, duration, record count, and newest event timestamp.

### EXAMPLE 2
```powershell
Test-EVXLog -LogName Security -MachineName DC1 -XPath '*[System[EventID=4624]]' -TimeoutMs 5000
```

Bounds session setup and event reading while distinguishing access, timeout, query, and no-event outcomes.

## PARAMETERS

### -Authentication
Authentication package for remote Event Log sessions.

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
Credentials for remote Event Log sessions.

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
Channels to probe.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
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

### -MaxEventsToScan
Maximum records inspected before reporting LimitReached.

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

### -TimeoutMs
Total probe budget in milliseconds.

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

### -XPath
Optional native XPath expression used to select the newest matching event.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String[]`

## OUTPUTS

- `EventViewerX.EventLogProbeResult`

## RELATED LINKS

- None
