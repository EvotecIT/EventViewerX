---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# Get-EVXCollectorSubscription
## SYNOPSIS
Returns normalized Windows Event Collector subscription configuration.

Reads local or remote WEC subscription inventory and returns detached snapshots with normalized XML details and query definitions. Remote access uses the caller's Windows identity.

## SYNTAX
### Subscriptions (Default)
```powershell
Get-EVXCollectorSubscription [[-Name] <string[]>] [-MachineName <string[]>] [-EnabledOnly] [-IncludeRuntimeStatus] [<CommonParameters>]
```

### Readiness
```powershell
Get-EVXCollectorSubscription -Readiness [<CommonParameters>]
```

## DESCRIPTION
Returns normalized Windows Event Collector subscription configuration.

Reads local or remote WEC subscription inventory and returns detached snapshots with normalized XML details and query definitions. Remote access uses the caller's Windows identity.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXCollectorSubscription -EnabledOnly
```

Returns only enabled subscriptions from the local collector.

### EXAMPLE 2
```powershell
Get-EVXCollectorSubscription -Name '*Domain Controllers*' -MachineName WEC01
```

Uses Remote Registry access under the current Windows identity and applies wildcard matching to detached snapshots.

### EXAMPLE 3
```powershell
Get-EVXCollectorSubscription -Readiness
```

Reports the WEC service, WinRM listener, ForwardedEvents channel, elevation, and actionable readiness issues.

### EXAMPLE 4
```powershell
Get-EVXCollectorSubscription -Name 'Domain controller authentication' -IncludeRuntimeStatus
```

Adds processed-event counters, source heartbeat timestamps, and native Windows errors to the local snapshot.

## PARAMETERS

### -EnabledOnly
Returns only enabled subscriptions.

```yaml
Type: SwitchParameter
Parameter Sets: Subscriptions
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeRuntimeStatus
Includes current per-source runtime state and Windows error details. Runtime status is local-only.

```yaml
Type: SwitchParameter
Parameter Sets: Subscriptions
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Collector computers. Omit for the local computer.

```yaml
Type: String[]
Parameter Sets: Subscriptions
Aliases: ComputerName, ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Name
Subscription names or wildcard patterns.

```yaml
Type: String[]
Parameter Sets: Subscriptions
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Readiness
Returns local WEC, WinRM listener, and ForwardedEvents readiness instead of subscription inventory.

```yaml
Type: SwitchParameter
Parameter Sets: Readiness
Aliases: None
Possible values:

Required: True
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

- `EventViewerX.CollectorSubscriptionSnapshot`
- `EventViewerX.CollectorReadinessStatus`

## RELATED LINKS

- None
