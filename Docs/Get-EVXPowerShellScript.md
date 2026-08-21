---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/PSEventViewer
schema: 2.0.0
---
# Get-EVXPowerShellScript
## SYNOPSIS
Retrieves reconstructed PowerShell scripts or execution-context records from event logs.

## SYNTAX
### Script (Default)
```powershell
Get-EVXPowerShellScript [-Edition] <PowerShellEdition> [-OutputPath <string>] [-Format] [-ContainsText <string[]>] [-MaxScripts <int>] [-MaxPendingScripts <int>] [-MaxCachedEvents <int>] [-MachineName <string[]>] [-EventLogPath <string>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEventsScanned <int>] [-IncludeQueryInfo] [<CommonParameters>]
```

### Execution
```powershell
Get-EVXPowerShellScript [-Edition] <PowerShellEdition> -Execution [-MaxEvents <int>] [-MachineName <string[]>] [-EventLogPath <string>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-TimePeriod <TimePeriod>] [-MaxEventsScanned <int>] [-IncludeQueryInfo] [<CommonParameters>]
```

## DESCRIPTION
Retrieves reconstructed PowerShell scripts or execution-context records from event logs.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXPowerShellScript -Edition WindowsPowerShell -MachineName DC01,DC02 -OutputPath C:\RecoveredScripts -MaxScripts 100
```

Reconstructs scripts and streams the saved paths.

### EXAMPLE 2
```powershell
Get-EVXPowerShellScript -Execution -Edition WindowsPowerShell -MachineName DC01 -MaxEvents 100
```

Selects the execution parameter set without introducing another cmdlet.

## PARAMETERS

### -ContainsText
Filters scripts to those containing the specified text.

```yaml
Type: String[]
Parameter Sets: Script
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Edition
PowerShell edition whose operational log should be queried.

```yaml
Type: PowerShellEdition
Parameter Sets: Script, Execution
Aliases: Type
Possible values: PowerShell, WindowsPowerShell

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Only reads events logged before this date.

```yaml
Type: DateTime
Parameter Sets: Script, Execution
Aliases: DateTo
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventLogPath
Exported EVTX file to query locally instead of a live operational log.
This cannot be combined with MachineName.

```yaml
Type: String
Parameter Sets: Script, Execution
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Execution
Returns execution-context records instead of reconstructed script text.

```yaml
Type: SwitchParameter
Parameter Sets: Execution
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format
When set, converts scripts back to their original formatting.

```yaml
Type: SwitchParameter
Parameter Sets: Script
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeQueryInfo
Emits a machine-readable completion record after each computer.

```yaml
Type: SwitchParameter
Parameter Sets: Script, Execution
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MachineName
Computer names to query. When omitted, the local machine is used.
This cannot be combined with EventLogPath.

```yaml
Type: String[]
Parameter Sets: Script, Execution
Aliases: ComputerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxCachedEvents
Maximum event snapshots retained across incomplete script groups.

```yaml
Type: Int32
Parameter Sets: Script
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEvents
Maximum execution-context records to return per computer. Zero returns every match.

```yaml
Type: Int32
Parameter Sets: Execution
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEventsScanned
Maximum native records to scan per computer. Zero scans the complete query.

```yaml
Type: Int32
Parameter Sets: Script, Execution
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxPendingScripts
Maximum incomplete script groups retained while scanning.

```yaml
Type: Int32
Parameter Sets: Script
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxScripts
Maximum reconstructed scripts to return per computer. Zero returns every matching script.

```yaml
Type: Int32
Parameter Sets: Script
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Destination directory where retrieved scripts should be saved.

```yaml
Type: String
Parameter Sets: Script
Aliases: Path
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Only reads events logged after this date.

```yaml
Type: DateTime
Parameter Sets: Script, Execution
Aliases: DateFrom
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimePeriod
Reusable relative time window. This cannot be combined with StartTime or EndTime.

```yaml
Type: TimePeriod
Parameter Sets: Script, Execution
Aliases: None
Possible values: PastHour, CurrentHour, PastDay, CurrentDay, PastMonth, CurrentMonth, PastQuarter, CurrentQuarter, Last3Days, Last7Days, Last14Days, Last1Hour, Last2Hours, Last3Hours, Last6Hours, Last12Hours, Last16Hours, Last24Hours, Today, Yesterday, Everything, TillLastMonday, TillLastTuesday, TillLastWednesday, TillLastThursday, TillLastFriday, TillLastSaturday, TillLastSunday, Last15Minutes, Last30Minutes

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

- `EventViewerX.RestoredPowerShellScript`
- `EventViewerX.PowerShellScriptExecutionInfo`
- `EventViewerX.PowerShellScriptQueryExecutionInfo`
- `System.String`

## RELATED LINKS

- None
