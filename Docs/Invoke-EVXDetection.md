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

Storage is optional. Pipe events directly from Get-EVXEvent, supply detached EventObject instances, or use FromStore to rebuild stateful correlation across process restarts.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-EVXDetection [-InputObject <Object>] [-FromStore <string>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-Rule <IEventDetectionRule[]>] [-Pack <EventDetectionPack[]>] [-IncludeBuiltIn] [-Tuning <EventDetectionTuning>] [-Coverage <EventDetectionCoverage>] [-Explain] [-Trace] [-ReportKind <EventDecisionReportKind>] [-MaximumObservations <long>] [-MaximumCandidates <long>] [-MaximumGroups <int>] [-MaximumStateObservations <int>] [-MaximumStateBytes <long>] [<CommonParameters>]
```

## DESCRIPTION
Evaluates EventViewerX events with native detection and correlation rules.

Compiles one immutable indexed plan, projects each raw event once, and emits explainable findings with evidence and pack provenance.

Storage is optional. Pipe events directly from Get-EVXEvent, supply detached EventObject instances, or use FromStore to rebuild stateful correlation across process restarts.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXEvent -Type ActiveDirectoryAuthentication -TimePeriod Last24Hours -Oldest | Invoke-EVXDetection
```

Evaluates the built-in native packs and emits findings as typed objects. Materialized input is normalized to deterministic event-time order before correlation.

### EXAMPLE 2
```powershell
Invoke-EVXDetection -FromStore C:\Data\events.db -StartTime (Get-Date).AddHours(-1) -Coverage $coverage
```

Loads the requested window plus the plan's required stateful lookback and emits only findings that end in the requested window.

### EXAMPLE 3
```powershell
$tuning = [EventViewerX.EventDetectionTuning]::new(); $tuning.DisabledRuleIds = 'EVX-AUTH-0003'; Get-EVXEvent -Type ActiveDirectoryAuthentication -Oldest | Invoke-EVXDetection -Tuning $tuning
```

Disables a rule without changing the versioned pack content.

### EXAMPLE 4
```powershell
Invoke-EVXDetection -Explain
```

Returns selectors, state requirements, and required typed projections without processing events.

## PARAMETERS

### -Coverage
Expected and successfully collected source scope attached to every finding.

```yaml
Type: EventDetectionCoverage
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
UTC or local upper boundary for historical findings.

```yaml
Type: DateTime
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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

### -FromStore
Optional EventStore database used as the historical source instead of pipeline input.

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
Detached, typed, or custom EventViewerX event to evaluate.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaximumCandidates
Maximum stored candidate rows inspected before exact evaluation.

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

### -ReportKind
Returns one decision-oriented report snapshot instead of individual findings.

```yaml
Type: EventDecisionReportKind
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: CollectionCoverage, EventingIntegrity, AuthenticationPosture, IdentityLifecycle, PrivilegedAccess, GroupPolicyGovernance, CertificateServicesGovernance, ExecutionAndPersistence, DetectionHealth, UnknownEventAndSchemaDrift, IncidentTimeline

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

### -StartTime
UTC or local lower boundary for historical findings. Stateful lookback is loaded automatically.

```yaml
Type: DateTime
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Trace
Returns a per-observation rule decision trace instead of findings.

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

- `System.Object`

## OUTPUTS

- `EventViewerX.EventDetectionFinding`
- `EventViewerX.EventDetectionPlanExplanation`
- `EventViewerX.EventDecisionReportSnapshot`
- `EventViewerX.EventDetectionRuleTrace`

## RELATED LINKS

- None
