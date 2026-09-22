---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXKerberosImpact
## SYNOPSIS
Summarizes observed KDCsvc RC4 enforcement impact by domain controller, account, and service.

Distinguishes audit warnings, requests already blocked, and explicit insecure defaults. Event evidence does not certify complete domain coverage.

## SYNTAX
### Store (Default)
```powershell
Get-EVXKerberosImpact [-FromStore] <string> [-StartTime <DateTime>] [-EndTime <DateTime>] [-MaxEvents <long>] [-MaximumGroups <int>] [-MaximumEvidencePerGroup <int>] [<CommonParameters>]
```

### Report
```powershell
Get-EVXKerberosImpact -Report <EventReport> [-MaximumGroups <int>] [-MaximumEvidencePerGroup <int>] [<CommonParameters>]
```

## DESCRIPTION
Summarizes observed KDCsvc RC4 enforcement impact by domain controller, account, and service.

Distinguishes audit warnings, requests already blocked, and explicit insecure defaults. Event evidence does not certify complete domain coverage.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXKerberosImpact -FromStore C:\Data\events.db -StartTime (Get-Date).AddDays(-7)
```


## PARAMETERS

### -EndTime
Inclusive upper event-time boundary for stored history.

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

### -FromStore
EventStore SQLite database path.

```yaml
Type: String
Parameter Sets: Store
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEvents
Maximum stored rows examined; zero selects every match.

```yaml
Type: Int64
Parameter Sets: Store
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumEvidencePerGroup
Maximum evidence identities retained per impact group.

```yaml
Type: Int32
Parameter Sets: Store, Report
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumGroups
Maximum distinct impact groups retained.

```yaml
Type: Int32
Parameter Sets: Store, Report
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Report
Existing report of typed KDCsvc events.

```yaml
Type: EventReport
Parameter Sets: Report
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -StartTime
Inclusive lower event-time boundary for stored history.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `EventViewerX.Reporting.EventReport`

## OUTPUTS

- `EventViewerX.KerberosRc4ImpactReport`

## RELATED LINKS

- None
