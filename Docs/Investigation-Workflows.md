# Five event-investigation workflows

Use these jobs to collect a bounded snapshot, inspect its evidence and produce a
report. They use the installed `PSEventViewer` module on Windows PowerShell 5.1
or PowerShell 7. Choose explicit source computers or a Windows Event Collector;
domain and forest discovery remain opt-in.

## Install and choose the output directory

```powershell
Install-Module -Name PSEventViewer -Scope CurrentUser -Force
Import-Module PSEventViewer
Get-Module PSEventViewer | Select-Object Name, Version, Path

$output = Join-Path $env:LOCALAPPDATA 'EventViewerX/Reports'
New-Item -Path $output -ItemType Directory -Force | Out-Null
$collector = 'WEC01'
```

Replace `WEC01` with your collector. For direct collection, replace
`-Collector $collector` on a query with `-MachineName DC01,DC02`. Use
[Onboarding](Onboarding.md) to assess direct-source audit policy, permissions and
transport before scheduling. Readiness commands inspect configuration; they do
not enable audit policy or change subscriptions.

## Investigate account lockouts

Inspect the requirements and your collector's evidence before collecting:

```powershell
Get-EVXRequirement -Type ADUserLockouts
$readiness = Test-EVXReadiness -Scenario AccountLockoutMonitoring -Collector $collector
$readiness.Checks | Format-Table Status, Target, Check, EvidenceLevel -AutoSize

$report = Show-EVXEvent -Type ADUserLockouts -Collector $collector `
    -TimePeriod Last24Hours -MaxEvents 10000 -MaxEventsScanned 100000 `
    -HtmlPath (Join-Path $output 'Lockouts.html') `
    -CsvPath (Join-Path $output 'Lockouts.csv') -PassThru
$report.Rows | Select-Object TimeCreated, SourceComputer, Values
```

Pivot on the affected account and caller computer in the interactive report.
Keep source computer and record ID: different domain controllers can record
separate observations of one lockout. Use an explicit occurrence policy when
grouping those observations; similar timestamps alone do not prove identity.

For a daily trend, keep the snapshot's coverage rather than piping loose rows:

```powershell
$report | Measure-EVXEvent -GroupBy Who -Bucket Day -Top 10 |
    Show-EVXEvent -HtmlPath (Join-Path $output 'Lockout-Trend.html')
```

Inspect the section's field names before choosing a different grouping key.

## Find authentication-modernization evidence

```powershell
Get-EVXRequirement -Type AuthenticationHealth
$readiness = Test-EVXReadiness -Scenario AuthenticationMonitoring -Collector $collector
$readiness.Checks | Format-Table Status, Target, Check, EvidenceLevel -AutoSize

$selection = [EventViewerX.EventMonitoringPresetCatalog]::Get(
    [EventViewerX.EventMonitoringPreset]::AuthenticationHealth)
$report = Show-EVXEvent -Type $selection.Types -Where $selection.Predicate -Collector $collector `
    -TimePeriod Last7Days -MaxEvents 10000 -MaxEventsScanned 100000 `
    -HtmlPath (Join-Path $output 'Authentication.html') `
    -ExcelPath (Join-Path $output 'Authentication.xlsx') -PassThru
```

The preset selects NTLMv1, weak Kerberos encryption and LDAP signing/binding
signals through their typed definitions. Missing encryption fields remain
unknown. Review the source, account, service and event-local evidence before
changing authentication policy. The report does not enumerate directory
configuration or decide whether an exception is acceptable.

[Operational packs](Operational-Packs.md) provide versioned detections over
these observations. Review their required coverage before interpreting an empty
finding set as a clean environment.

## Retain Group Policy names and changes

```powershell
Get-EVXRequirement -Type GroupPolicyDirectoryAudit
$readiness = Test-EVXReadiness -Scenario GroupPolicyMonitoring -Collector $collector
$readiness.Checks | Format-Table Status, Target, Check, EvidenceLevel -AutoSize

$report = Show-EVXEvent -Type GroupPolicyDirectoryAudit -Collector $collector `
    -TimePeriod Last24Hours -MaxEvents 10000 -MaxEventsScanned 100000 `
    -ContextStorePath (Join-Path $output 'gpo-context.db') `
    -StorePath (Join-Path $output 'gpo-history.db') `
    -HtmlPath (Join-Path $output 'Group-Policy.html') -PassThru
```

The context store learns display names from selected events and retains them
across rename and deletion. A GUID whose name was never observed remains
unresolved; the report does not silently inventory AD or SYSVOL. Start retention
before an investigation needs deleted-object context. Keep unknown names visible
beside the original GUID and evidence.

## Check collector health and coverage

```powershell
Get-EVXCollectorSubscription -MachineName $collector -IncludeRuntimeStatus
$readiness = Test-EVXReadiness -Scenario DailyActiveDirectoryReport `
    -Collector $collector -SubscriptionName 'Domain controller changes' `
    -ExpectedSource DC01,DC02
$readiness.Checks | Format-Table Layer, Status, Target, Check, EvidenceLevel -AutoSize
$readiness | Select-Object IsReady, IsComplete, RequiredFailures, UnknownRequiredChecks

$report = Show-EVXEvent -Type ActiveDirectoryChanges -Collector $collector `
    -TimePeriod Last24Hours -MaxEvents 10000 -MaxEventsScanned 100000 `
    -HtmlPath (Join-Path $output 'Collector-Changes.html') -PassThru
```

Supply the actual subscription name and expected sources. A responding collector
does not prove that every domain controller forwards events, that required audit
policy is enabled, or that the log retains the requested period. Follow
[WEC fleet operations](WEC-Fleet-Operations.md) for source runtime status, lag,
subscription drift and end-to-end coverage proof.

## Investigate a saved EVTX file

Use an explicit path and bounded row count. No collector or directory discovery is
needed:

```powershell
$path = 'C:/Evidence/Security.evtx'
$report = Show-EVXEvent -Path $path -Oldest -MaxEvents 10000 `
    -MaxEventsScanned 100000 -HtmlPath (Join-Path $output 'Saved-Events.html') `
    -StorePath (Join-Path $output 'saved-history.db') -PassThru
$report.Sections | Select-Object Name, DisplayName, Columns

$stored = Show-EVXEvent -FromStore (Join-Path $output 'saved-history.db') -PassThru
$stored.Rows.Count
Test-EVXStore -Path (Join-Path $output 'saved-history.db')
```

Use `-Type` when a supported typed schema is needed, or a declarative
[event definition](Event-Definitions.md) for a provider-specific schema.
Preserve the original EVTX file. A report can describe only the retained events;
it cannot reconstruct records removed before collection.

## Make automation distinguish empty, incomplete and failed input

After any snapshot job, save its operational evidence next to the report:

```powershell
$report | Select-Object Title, GeneratedAt, QueryDuration, EventsScanned, `
    ScanLimitReached, CompletenessDiagnostic, Coverage |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $output 'Collection-Status.json') -Encoding UTF8

$failedSources = @($report.Coverage | Where-Object { -not $_.Succeeded })
if ($report.ScanLimitReached -or $failedSources.Count -gt 0 -or
    -not [string]::IsNullOrWhiteSpace($report.CompletenessDiagnostic)) {
    throw 'Collection is incomplete; inspect Collection-Status.json before treating the result as exhaustive.'
}
```

An empty complete snapshot means no matching events were found in the selected
retained input. An incomplete snapshot cannot support that conclusion. A
readiness `Unknown` requires evidence; it is neither pass nor fail. Preserve
aggregation `InputCompleteness`, `AggregationComplete` and `Diagnostic`, and
detection coverage when passing results to another application.

Run scheduled jobs under the intended identity and test their exact source
permissions. Start with bounded windows and HTML detail plus compact email;
use stored history and aggregation for long periods. The examples do not create
scheduled tasks, send mail or change Windows configuration. Use
[Onboarding](Onboarding.md) for the scheduling and delivery contract.
