# Migrating to EventViewerX 4.0

EventViewerX 4.0 is the active successor to PSWinReporting and PSWinReportingV2. The legacy modules remain available from the frozen `PSWinReporting` and `PSWinReportingV2` branches, but new reporting and monitoring work belongs in PSEventViewer and the EventViewerX engine.

This guide describes the intended 4.0 contract. Do not remove a working legacy schedule until its EventViewerX replacement passes readiness and produces an accepted report from the same sources.

## Package and runtime names

- PowerShell: install and import `PSEventViewer`.
- .NET: reference `EventViewerX`, `EventViewerX.Reporting`, and `EventViewerX.Storage` as needed.
- CLI: use `evx.exe` from the EventViewerX CLI artifact.
- Windows PowerShell 5.1 and PowerShell 7 expose the same module commands and result contracts.

The three EventViewerX NuGet packages use one release version. PSWinReporting 1.x and PSWinReportingV2 2.x keep their historical versions and are not upgraded into EventViewerX 4.0.

## Choose the collection path first

| Need | EventViewerX path |
| --- | --- |
| One local computer | Omit `-MachineName`; local is the default |
| Several known computers | `-MachineName` with explicit names |
| Current AD domain or forest | `Get-EVXTarget -ActiveDirectory CurrentDomain` or `CurrentForest`, then pass the successful targets |
| Central Windows Event Collector | `-Collector` and a verified subscription |
| Offline investigation | `-Path` with one or more EVTX files |
| Multi-run history and trends | Write normalized reports to `EventStore`, then query or measure the SQLite store |

Current forest and trusted forests are never implicit. Trusted-forest traversal requires its explicit option and returns per-domain failures rather than hiding partial discovery.

## Prove prerequisites before scheduling

Use `Get-EVXRequirement` to inspect what a type needs and `Test-EVXReadiness` to test the actual source/collector identity. Follow [Onboarding and prerequisites](Onboarding.md) for audit policy, event IDs, permissions, firewall, WEC, and scheduled-task setup.

Readiness is diagnostic. It uses the caller's available permissions and reports `Pass`, `Fail`, `Unknown`, and typed evidence; it does not silently configure Windows.

## Replace custom report scripts with typed definitions and presets

PSWinReporting commonly required custom script blocks because each report owned its own query and formatting logic. EventViewerX uses compiled event types, exact predicates, shared reports, and built-in presets.

```powershell
# Weak authentication signals: NTLMv1, RC4/DES Kerberos, and LDAP signing.
Get-EVXEvent -Preset AuthenticationHealth -TimePeriod Last24Hours

# Scheduled task, firewall, or Defender monitoring.
Get-EVXEvent -Preset ScheduledTaskActivity -TimePeriod Last7Days
Get-EVXEvent -Preset FirewallRuleActivity -TimePeriod Last7Days
Get-EVXEvent -Preset DefenderSecurity -TimePeriod Last7Days
```

Portable custom event definitions remain declarative JSON. They cannot execute arbitrary PowerShell or C# code.

## Generate one report snapshot and reuse it

```powershell
$report = Show-EVXEvent -Type ActiveDirectoryAuthentication `
    -Collector WEC01 -TimePeriod Last24Hours `
    -HtmlPath .\Authentication.html `
    -ExcelPath .\Authentication.xlsx `
    -CsvPath .\Authentication.zip `
    -PassThru
```

All formats consume the same normalized rows and source-coverage evidence. A failed target remains visible without discarding successful independent targets.

## Understand normalization

Known directory and audit values expose a canonical typed value and readable display text while retaining the provider's raw value, normalizer/version, outcome, warnings, and lossless status. Unknown or malformed values are not silently replaced.

CSV adds a raw companion field when canonical output differs. PowerShell and .NET callers can inspect `EventReportRow.Values` for raw evidence and `NormalizedValues` for canonical values.

CLI query rows keep event fields flat and place normalization evidence under
`_EventViewerX.Normalization`. The `_EventViewerX` name is reserved in custom
definition fields and aliases so EventViewerX metadata can never overwrite a
declared event value. A custom field named `Normalization` remains valid.

## Group observations without deleting evidence

Occurrence grouping is a view:

- transport grouping joins only exact source-event identity duplicated through direct/WEC collection;
- semantic grouping requires a compiled causal identifier and an explicit window;
- every group retains all observations and a deterministic representative;
- time proximity alone never joins events.

General incident reconstruction is not part of 4.0.

## Aggregate and chart consistently

```powershell
$trend = Get-EVXEvent -Preset AuthenticationHealth -TimePeriod Last7Days |
    Measure-EVXEvent -GroupBy Type,SourceComputer -Bucket Day -Top 10

$trend | Show-EVXEvent `
    -HtmlPath .\Authentication-Trend.html `
    -ExcelPath .\Authentication-Trend.xlsx `
    -CsvPath .\Authentication-Trend.csv
```

For stored history, use `Measure-EVXEvent -FromStore`. EventViewerX selects SQLite pushdown only when it can preserve the exact managed semantics; otherwise it falls back automatically. Inspect `ExecutionMode`, `InputCompleteness`, `AggregationComplete`, and `Diagnostic` before treating a result as exhaustive.

## Preserve Group Policy names across deletion and rename

```powershell
Show-EVXEvent -Type GroupPolicyDirectoryAudit `
    -ContextStorePath C:\ProgramData\EventViewerX\context.db `
    -TimePeriod Last30Days `
    -HtmlPath .\GroupPolicy-Audit.html `
    -ExcelPath .\GroupPolicy-Audit.xlsx
```

The context database is populated only from selected Group Policy audit events. It resolves name-at-event-time, last-known name, current name, state, and reason. It does not enumerate AD or SYSVOL. Keep the database across scheduled runs; deleting it discards the historical context that later deletion events may need.

## Scheduling cutover checklist

- [ ] The selected event types and sources pass readiness for the unattended identity.
- [ ] The first EventViewerX collection covers the intended direct or WEC sources.
- [ ] The SQLite event/context databases are on durable storage with appropriate ACLs and backup policy.
- [ ] A manual report produces accepted HTML/Excel/CSV output.
- [ ] A one-time scheduled readiness/report task succeeds under the real identity.
- [ ] The first closed reporting window is complete; a partial onboarding day is not mistaken for a complete daily report.
- [ ] Only then disable the matching PSWinReporting or PSWinReportingV2 task.

## Intentionally deferred

EventViewerX 4.0 does not reserve incomplete public contracts for incidents, built-in notification transports, custom branding/licensing, SQL Server central storage, a Windows service agent, or arbitrary user code inside normalization/grouping. See the [roadmap](../ROADMAP.md) for the current decision boundary.
