---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Measure-EVXEvent
## SYNOPSIS
Computes bounded event counts, distinct values, first/last observations, rates, and time trends.

Uses the shared deterministic aggregation contract for pipeline events and safe SQLite pushdown for stored history.

A single EventReport preserves its source-coverage evidence. Individual pipeline rows have unknown completeness because a pipeline cannot prove that it contains the complete source query.

## SYNTAX
### Input (Default)
```powershell
Measure-EVXEvent -InputObject <Object> [-GroupBy <string[]>] [-Bucket <EventAggregationBucket>] [-TimeZoneId <string>] [-Measure <Object[]>] [-Top <int>] [-TopScope <EventAggregationTopScope>] [-RankingMeasure <string>] [-WindowStart <DateTime>] [-WindowEnd <DateTime>] [-MaximumGroups <int>] [-MaximumDistinctValues <int>] [-MaximumStateBytes <long>] [<CommonParameters>]
```

### Store
```powershell
Measure-EVXEvent -FromStore <string> [-Type <EventType[]>] [-Preset <EventMonitoringPreset>] [-DefinitionName <string[]>] [-StartTime <DateTime>] [-EndTime <DateTime>] [-SourceComputer <string[]>] [-GroupBy <string[]>] [-Bucket <EventAggregationBucket>] [-TimeZoneId <string>] [-Measure <Object[]>] [-Top <int>] [-TopScope <EventAggregationTopScope>] [-RankingMeasure <string>] [-WindowStart <DateTime>] [-WindowEnd <DateTime>] [-MaximumGroups <int>] [-MaximumDistinctValues <int>] [-MaximumStateBytes <long>] [-Explain] [<CommonParameters>]
```

## DESCRIPTION
Computes bounded event counts, distinct values, first/last observations, rates, and time trends.

Uses the shared deterministic aggregation contract for pipeline events and safe SQLite pushdown for stored history.

A single EventReport preserves its source-coverage evidence. Individual pipeline rows have unknown completeness because a pipeline cannot prove that it contains the complete source query.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXEvent -Type ADUserLogonFailed -TimePeriod Last24Hours | Measure-EVXEvent -GroupBy Who
```

Returns one completeness-aware aggregation result containing deterministic rows.

### EXAMPLE 2
```powershell
Measure-EVXEvent -FromStore C:\EventViewerX\events.db -Preset AuthenticationHealth -Bucket Hour -GroupBy Type
```

Applies the exact weak-authentication preset predicate and uses SQLite pushdown only when the selected fields preserve the shared semantics.

### EXAMPLE 3
```powershell
Get-EVXEvent -Preset AuthenticationHealth -TimePeriod Last7Days | Measure-EVXEvent -GroupBy Type -Measure 'Count', 'DistinctCount:SourceComputer:Sources'
```

String measure specifications use Operation:Field:OutputName:RateUnit and can be mixed with typed measure objects or hashtables.

## PARAMETERS

### -Bucket
Calendar trend bucket.

```yaml
Type: EventAggregationBucket
Parameter Sets: Input, Store
Aliases: None
Possible values: None, Hour, Day, Week, Month

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DefinitionName
Stored definition names to include.

```yaml
Type: String[]
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Stored event upper time boundary.

```yaml
Type: DateTime
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Explain
Returns the selected stored execution owner without aggregating.

```yaml
Type: SwitchParameter
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FromStore
SQLite EventStore path.

```yaml
Type: String
Parameter Sets: Store
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GroupBy
Canonical dimensions forming each group key.

```yaml
Type: String[]
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Event rows, typed EventViewerX records, or one EventReport to aggregate. Supply an EventReport alone to preserve its completeness envelope.

```yaml
Type: Object
Parameter Sets: Input
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaximumDistinctValues
Maximum distinct values retained per measure.

```yaml
Type: Int32
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumGroups
Maximum aggregation groups retained.

```yaml
Type: Int32
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumStateBytes
Maximum approximate managed aggregation-state bytes.

```yaml
Type: Int64
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Measure
Typed measures, hashtables, or Operation:Field:OutputName:RateUnit strings. Count is used by default.

```yaml
Type: Object[]
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Preset
Stored monitoring preset whose event types and exact semantic predicate are applied together.

```yaml
Type: EventMonitoringPreset
Parameter Sets: Store
Aliases: None
Possible values: AuthenticationHealth, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RankingMeasure
Measure output used for top-N ranking.

```yaml
Type: String
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceComputer
Stored source computers to include.

```yaml
Type: String[]
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Stored event lower time boundary.

```yaml
Type: DateTime
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeZoneId
Timezone used for calendar buckets. UTC enables stored pushdown.

```yaml
Type: String
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Top
Maximum ranked groups returned. Zero returns every group.

```yaml
Type: Int32
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TopScope
Global or per-bucket top-N scope.

```yaml
Type: EventAggregationTopScope
Parameter Sets: Input, Store
Aliases: None
Possible values: GlobalGroup, PerBucket

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Stored built-in event types to include.

```yaml
Type: EventType[]
Parameter Sets: Store
Aliases: None
Possible values: ADComputerCreateChange, ADComputerDeleted, ADComputerChangeDetailed, ADGroupMembershipChange, ADGroupEnumeration, ADGroupChange, ADGroupCreateDelete, ADGroupChangeDetailed, ADGroupPolicyChanges, ADGroupPolicyEdits, ADGroupPolicyLinks, ADGroupPolicyChangesDetailed, GpoCreated, GpoDeleted, GpoModified, ADLdapBindingSummary, ADLdapBindingDetails, ADUserCreateChange, ADUserStatus, ADUserChangeDetailed, ADUserLockouts, ADUserLogon, ADUserLogonNTLMv1, ADUserLogonFailed, ADUserUnlocked, ADUserPrivilegeUse, ADUserRightsAssignment, KerberosTGTRequest, KerberosServiceTicket, KerberosTicketFailure, KerberosPolicyChange, KerberosKdcRc4Audit, ADOrganizationalUnitChangeDetailed, ADOtherChangeDetailed, ADSMBServerAuditV1, LogsClearedSecurity, LogsClearedOther, LogsFullSecurity, NetworkAccessAuthenticationPolicy, CertificateIssued, AuditPolicyChange, FirewallRuleChange, DhcpLeaseCreated, BitLockerKeyChange, BitLockerSuspended, DeviceRecognized, DeviceDisabled, ObjectDeletion, ScheduledTaskDeleted, ScheduledTaskCreated, OSCrash, OSBugCheck, OSStartup, OSShutdown, OSUncleanShutdown, OSStartupSecurity, OSCrashOnAuditFailRecovery, OSTimeChange, WindowsUpdateFailure, ClientGroupPoliciesApplication, ClientGroupPoliciesSystem, HyperVVirtualMachineShutdown, HyperVVirtualMachineStarted, IISSiteBindingFailure, HyperVCheckpointCreated, IISSiteStopped, ExchangeDatabaseMounted, DfsReplicationError, SqlDatabaseCreated, SyncCompleted, AADConnectStagingEnabled, AADConnectStagingDisabled, AADConnectPasswordSyncFailed, AADConnectRunProfile, AADSyncCycleStage, AADSyncProvisionCredentialsPing, AADSyncPasswordHashSyncStatus, AADSyncImportStatus, AADSyncFilterStatus, NetworkMonitorDriverLoaded, NetworkPromiscuousMode, ActiveDirectoryAuthentication, ActiveDirectoryAccountLifecycle, ActiveDirectoryChanges, GroupPolicyActivity, KerberosActivity, OperatingSystemLifecycle, WindowsSecurityChanges, EntraConnectHealth, NetworkSecurity, InfrastructureHealth, ScheduledTaskEnabled, ScheduledTaskDisabled, ScheduledTaskUpdated, FirewallRuleAdded, FirewallRuleDeleted, DefenderThreatDetected, DefenderThreatAction, DefenderConfigurationChanged, ScheduledTaskActivity, FirewallRuleActivity, DefenderSecurity, AuthenticationHealth, GroupPolicyDirectoryAudit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WindowEnd
Explicit unbucketed rate interval end.

```yaml
Type: DateTime
Parameter Sets: Input, Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WindowStart
Explicit unbucketed rate interval start.

```yaml
Type: DateTime
Parameter Sets: Input, Store
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

- `EventViewerX.Reporting.EventAggregationResult`
- `EventViewerX.Storage.EventStoreAggregationPlan`

## RELATED LINKS

- None
