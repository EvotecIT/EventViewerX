---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Set-EVXCollectorSubscription
## SYNOPSIS
Applies a typed local WEC subscription definition or changes its enabled state.

Definition input creates or updates a subscription through the Windows inbox collector utility. The state set uses the supported WEC API. Both paths verify persisted state. Definition apply is cancellable and time-bounded; failed apply is rolled back and reports explicitly when rollback cannot establish a known persisted state.

## SYNTAX
### Enabled
```powershell
Set-EVXCollectorSubscription [-Name] <string> -Enabled <bool> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Remove
```powershell
Set-EVXCollectorSubscription [-Name] <string> -Remove [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Definition
```powershell
Set-EVXCollectorSubscription -Definition <CollectorSubscriptionDefinition> [-InitializeCollector] [-SkipWinRmQuickConfig] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Initialize
```powershell
Set-EVXCollectorSubscription -InitializeCollector [-SkipWinRmQuickConfig] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Applies a typed local WEC subscription definition or changes its enabled state.

Definition input creates or updates a subscription through the Windows inbox collector utility. The state set uses the supported WEC API. Both paths verify persisted state. Definition apply is cancellable and time-bounded; failed apply is rolled back and reports explicitly when rollback cannot establish a known persisted state.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-EVXCollectorSubscription -Name 'Domain Controllers' -Enabled $false
```

Returns before and after snapshots plus whether the persisted state changed.

### EXAMPLE 2
```powershell
New-EVXCollectorSubscription -Name FailedLogons -SourceComputer DC01,DC02 -LogName Security -EventId 4625 | Set-EVXCollectorSubscription
```

Applies the typed definition transactionally and verifies the persisted Windows configuration.

### EXAMPLE 3
```powershell
Set-EVXCollectorSubscription -Name FailedLogons -Remove
```

Deletes the local subscription through the inbox collector utility and verifies that it is absent.

### EXAMPLE 4
```powershell
$definition | Set-EVXCollectorSubscription -InitializeCollector -Confirm:$false
```

Runs the inbox collector quick configuration, verifies readiness, and then transactionally applies the definition.

## PARAMETERS

### -Definition
Typed subscription definition produced by New-EVXCollectorSubscription.

```yaml
Type: CollectorSubscriptionDefinition
Parameter Sets: Definition
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Enabled
Desired enabled state.

```yaml
Type: Boolean
Parameter Sets: Enabled
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InitializeCollector
Runs the inbox WinRM and Windows Event Collector quick configuration and verifies readiness.

```yaml
Type: SwitchParameter
Parameter Sets: Definition, Initialize
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Exact local collector subscription name.

```yaml
Type: String
Parameter Sets: Enabled, Remove
Aliases: SubscriptionName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Remove
Removes the named local collector subscription.

```yaml
Type: SwitchParameter
Parameter Sets: Remove
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipWinRmQuickConfig
Skips WinRM quick configuration when initializing an already managed WinRM host.

```yaml
Type: SwitchParameter
Parameter Sets: Definition, Initialize
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
- `EventViewerX.CollectorSubscriptionDefinition`

## OUTPUTS

- `EventViewerX.CollectorSubscriptionUpdateResult`
- `EventViewerX.CollectorSubscriptionRemovalResult`
- `EventViewerX.CollectorSubscriptionSnapshot`
- `EventViewerX.CollectorReadinessStatus`

## RELATED LINKS

- None
