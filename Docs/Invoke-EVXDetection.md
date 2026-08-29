---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Invoke-EVXDetection
## SYNOPSIS
Evaluates EventViewerX events with native detection and correlation rules.

Compiles one immutable indexed plan, projects each raw event once, and emits explainable findings with evidence and pack provenance.

Storage is not required. Pipe events directly from Get-EVXEvent or supply an array of detached EventObject instances.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-EVXDetection [-InputObject <EventObject>] [-Rule <IEventDetectionRule[]>] [-Pack <EventDetectionPack[]>] [-IncludeBuiltIn] [-Tuning <EventDetectionTuning>] [-Explain] [-MaximumObservations <long>] [-MaximumGroups <int>] [-MaximumStateObservations <int>] [-MaximumStateBytes <long>] [<CommonParameters>]
```

## DESCRIPTION
Evaluates EventViewerX events with native detection and correlation rules.

Compiles one immutable indexed plan, projects each raw event once, and emits explainable findings with evidence and pack provenance.

Storage is not required. Pipe events directly from Get-EVXEvent or supply an array of detached EventObject instances.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXEvent -Type ActiveDirectoryAuthentication -TimePeriod Last24Hours -Oldest | Invoke-EVXDetection
```

Evaluates the built-in native packs and emits findings as typed objects. Materialized input is normalized to deterministic event-time order before correlation.

### EXAMPLE 2
```powershell
$tuning = [EventViewerX.EventDetectionTuning]::new(); $tuning.DisabledRuleIds = 'EVX-AUTH-0003'; Get-EVXEvent -Type ActiveDirectoryAuthentication -Oldest | Invoke-EVXDetection -Tuning $tuning
```

Disables a rule without changing the versioned pack content.

### EXAMPLE 3
```powershell
Invoke-EVXDetection -Explain
```

Returns selectors, state requirements, and required typed projections without processing events.

## PARAMETERS

### -Explain
Returns the effective compiled plan without evaluating input.

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

### -IncludeBuiltIn
Adds the built-in packs when explicit Rule or Pack values are supplied.

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

### -InputObject
Detached EventViewerX event to evaluate.

```yaml
Type: EventObject
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaximumGroups
Maximum active correlation groups.

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

### -MaximumObservations
Maximum observations evaluated. Zero is unlimited.

```yaml
Type: Int64
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumStateBytes
Maximum estimated correlation-state bytes.

```yaml
Type: Int64
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumStateObservations
Maximum observations retained across correlation state.

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

### -Pack
Explicit versioned packs. When omitted, the built-in packs are used.

```yaml
Type: EventDetectionPack[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Rule
Explicit native rules. When omitted, the built-in packs are used.

```yaml
Type: IEventDetectionRule[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tuning
Environment-specific disables, severity changes, thresholds, and suppressions.

```yaml
Type: EventDetectionTuning
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

- `EventViewerX.EventObject`

## OUTPUTS

- `EventViewerX.EventDetectionFinding`
- `EventViewerX.EventDetectionPlanExplanation`

## RELATED LINKS

- None
