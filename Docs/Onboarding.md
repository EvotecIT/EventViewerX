# EventViewerX onboarding and prerequisites

EventViewerX starts with the local machine. It never expands a readiness check
to the current domain or forest unless you ask for that scope explicitly. This
keeps an interactive check predictable on enterprise networks while still
making domain-wide planning discoverable.

This guide covers:

- local, domain, forest, and trusted-forest scope;
- evidence and permissions;
- the audit policy and channels needed by the daily Active Directory report;
- direct Event Log queries versus Windows Event Forwarding (WEF/WEC);
- a complete scheduled collection and daily report workflow.

The diagnostic commands are read-only. They do not enable audit policy, change
channel ACLs, open firewall rules, create WEC subscriptions, or register
scheduled tasks.

## Install and inspect the local machine

The PowerShell module remains named `PSEventViewer`:

```powershell
Install-Module -Name PSEventViewer -Scope CurrentUser
Import-Module PSEventViewer
```

Start with the local target and a focused scenario:

```powershell
$target = Get-EVXTarget
$readiness = Test-EVXReadiness -Scenario DailyActiveDirectoryReport

$target.Targets
$readiness.Checks |
    Sort-Object Layer, Target, Check |
    Format-Table Layer, Status, EvidenceLevel, Target, Check -AutoSize
```

`Pass` means the command directly inspected the stated evidence. `Warning`
means transport worked but the expected event has not been observed, or a
similar non-blocking condition exists. `Fail` is a proven missing requirement.
`Unknown` means the current identity or target did not allow EventViewerX to
prove the requirement. Unknown is intentionally different from both pass and
fail.

Use these report-level properties in automation:

```powershell
$readiness.IsReady
$readiness.IsComplete
$readiness.RequiredFailures
$readiness.UnknownRequiredChecks
```

A permission failure does not stop the remaining checks. EventViewerX retains
per-target transport, effective-policy, configuration, and discovery evidence
so the operator can see what the current identity could and could not assess.
`Status` remains `Unknown` when proof is unavailable; `DiagnosticKind`
separately distinguishes `AccessDenied`, timeout, missing configuration,
unavailable transport, no evidence, truncation, and other errors.

## Opt in to Active Directory discovery

Current-domain and current-forest discovery are explicit:

```powershell
# Computer domain only
$domain = Get-EVXTarget -ActiveDirectory CurrentDomain

# Every domain in the computer forest
$forest = Get-EVXTarget -ActiveDirectory CurrentForest

$forest.Domains |
    Select-Object DomainName, Succeeded, Targets, Failures
$forest.Failures
```

Named scopes are useful from an administration host or when the current
computer context is not the desired directory:

```powershell
$credential = Get-Credential

Get-EVXTarget `
    -ActiveDirectory Domain `
    -Name ad.example.com `
    -Credential $credential

Get-EVXTarget `
    -ActiveDirectory Forest `
    -Name example.com `
    -Credential $credential
```

Forest discovery does not traverse forest trusts by default. Trusted forests
are another explicit expansion. This version inspects only forests directly
trusted by the selected forest; it does not recursively walk a graph of trusts:

```powershell
Get-EVXTarget `
    -ActiveDirectory CurrentForest `
    -IncludeTrustedForests `
    -MaximumDomainCount 20 `
    -MaximumTargetCount 200
```

The result preserves each domain's successes and failures and includes a
stable target-set fingerprint. `IsTruncated` is true when a declared domain or
target cap stopped expansion. Review the resolved domain controllers and
fingerprint before using dynamic discovery for a scheduled direct-collection
job; a later topology change is then visible in retained job evidence.

`-TimeoutMs` is the total discovery budget in milliseconds and defaults to
30,000 ms. Domains completed before that budget remain in the result beside a
typed timeout failure. Windows directory APIs do
not offer safe interruption for every native call, so one call already in
progress may finish in the background; EventViewerX cancels further domain and
trust expansion as soon as control returns from it.

## Inspect the requirements before changing policy

`Get-EVXRequirement` returns the same compiled catalog used by readiness. It
is the source of truth for channels, event IDs, audit outcomes, expected
volume, target role, and Microsoft documentation links:

```powershell
$requirements = Get-EVXRequirement `
    -Type ActiveDirectoryChanges, ActiveDirectoryAuthentication

$requirements.Sources |
    Format-Table LogName, EventIds -AutoSize

$requirements.Prerequisites |
    Sort-Object Kind, Name -Unique |
    Format-Table Kind, Name, AuditOutcomes, AppliesTo, Volume -AutoSize
```

The daily AD-change scenario currently selects these channels and event IDs:

| Channel | Event IDs |
| --- | --- |
| Application | 4098 |
| Security | 4624, 4625, 4672, 4704, 4705, 4713, 4719, 4720, 4722-4729, 4730-4735, 4737, 4738, 4740-4754, 4756-4764, 4767-4772, 4784-4788, 4791, 5136, 5137, 5139, 5141 |
| System | 1085 |

Use the compiled command rather than copying the condensed ranges above into
a subscription. It returns the exact current IDs without documentation drift.

The applicable advanced audit-policy subcategories are:

| Subcategory | Outcomes | Applies to |
| --- | --- | --- |
| Audit Logon | Success and Failure | Computers whose logons are reported |
| Audit Special Logon | Success | Computers whose privileged logons are reported |
| Audit User Account Management | Success and Failure | Domain controllers |
| Audit Computer Account Management | Success | Domain controllers |
| Audit Security Group Management | Success | Domain controllers |
| Audit Distribution Group Management | Success | Domain controllers |
| Audit Authorization Policy Change | Success | Computers whose user-rights assignments are reported |
| Audit Authentication Policy Change | Success | Domain controllers |
| Audit Directory Service Changes | Success | Domain controllers |
| Audit Audit Policy Change | Success | Computers whose policy changes are reported |
| Audit Kerberos Authentication Service | Success and Failure | Domain controllers |
| Audit Kerberos Service Ticket Operations | Success and Failure | Domain controllers |

Events 5136, 5137, 5139, and 5141 also depend on auditing being configured on
the selected directory objects. Enabling the subcategory alone does not add
the required SACL. Scope directory-object auditing deliberately: broad SACLs
can generate substantial volume and may capture sensitive values.

Readiness reads effective local advanced audit policy through the Windows
audit API. It does not treat configured GPO, a historical event, or a
successful Event Log query as proof of a remote computer's effective policy.
Remote policy therefore remains `Unknown` until it is inspected on that
source.

## Choose direct collection or WEC

### Direct Event Log queries

Direct collection is a good fit for an interactive investigation, a small
fixed target set, or a temporary monitoring window.

Requirements:

- the task identity can read every required channel on each source;
- DNS resolves the fixed source names;
- the source permits the Windows Event Log remoting RPC traffic;
- the built-in **Remote Event Log Management** firewall rules, or an
  equivalently scoped RPC policy, allow the collector host;
- TCP 135 and the applicable dynamic RPC ports are reachable;
- the selected channels are enabled and retain enough history for the query
  interval.

Assess the exact target set before scheduling it:

```powershell
$targets = Get-EVXTarget -ActiveDirectory CurrentDomain
$computers = $targets.Targets.ComputerName

Test-EVXReadiness `
    -Type ActiveDirectoryChanges `
    -ActiveDirectory CurrentDomain

Show-EVXEvent `
    -Type ActiveDirectoryChanges `
    -MachineName $computers `
    -TimePeriod Last24Hours `
    -HtmlPath C:\Reports\EventViewerX\AD-Changes.html `
    -ExcelPath C:\Reports\EventViewerX\AD-Changes.xlsx
```

Do not put `CurrentForest` into an unattended job and assume its size is
stable. Resolve it, review failures and target count, then freeze the intended
machine list or move the workload to WEC.

### Windows Event Forwarding and Windows Event Collector

WEC is the recommended enterprise collection boundary. Source-initiated WEF
lets policy enroll computers while EventViewerX reads one bounded
`ForwardedEvents` channel and still filters each event's original channel.

Requirements include:

- WinRM and the forwarding client configured on event sources;
- the Windows Event Collector service configured on the collector;
- a source- or collector-initiated subscription containing the exact compiled
  EventViewerX query;
- an allowed-source ACL scoped to the intended computer accounts or groups;
- `NETWORK SERVICE` able to read the secured source channels; Microsoft calls
  out membership in the built-in Event Log Readers group for forwarding the
  Security log;
- HTTP 5985 or HTTPS 5986, certificates when HTTPS or cross-domain forwarding
  requires them, and adequate ForwardedEvents retention;
- the EventViewerX task identity able to read ForwardedEvents and write its
  store and report folders.

Microsoft's [source-initiated subscription guide](https://learn.microsoft.com/en-us/windows/win32/wec/setting-up-a-source-initiated-subscription)
covers the source, collector, Security-log, and cross-domain setup. The
[WEF intrusion-detection guidance](https://learn.microsoft.com/en-us/windows/security/operating-system-security/device-management/use-windows-event-forwarding-to-assist-in-intrusion-detection)
explains baseline versus targeted subscriptions and channel permissions.

EventViewerX can create and inspect subscription definitions without applying
them implicitly:

```powershell
$domainControllersSid = (Get-ADGroup 'Domain Controllers').SID.Value

$definition = New-EVXCollectorSubscription `
    -Name EventViewerX-ADChanges `
    -Type ActiveDirectoryChanges `
    -SubscriptionType SourceInitiated `
    -CollectorHostName WEC01.contoso.com `
    -AllowedSourceSid $domainControllersSid

$definition
$definition | Set-EVXCollectorSubscription

Get-EVXCollectorSubscription -Name EventViewerX-ADChanges
Test-EVXReadiness `
    -Type ActiveDirectoryChanges `
    -Collector WEC01 `
    -SubscriptionName EventViewerX-ADChanges `
    -ActiveDirectory CurrentDomain
```

Review `Get-Help New-EVXCollectorSubscription -Full` for the exact source ACL
shape supported by the installed version. Applying a subscription is an
explicit administrative operation and is separate from readiness.

## Complete daily AD-change report

This workflow uses WEC, the compiled `evx` host, and the EventViewerX SQLite
store. It contains no custom event parser, callback, or enrichment script.
Collection overlaps by five minutes; provenance-based deduplication makes the
overlap safe and avoids gaps between task runs.

### 1. Create protected state and report folders

Run once from an elevated PowerShell session on the collector:

```powershell
$state = 'C:\ProgramData\EventViewerX'
$reports = 'C:\Reports\EventViewerX'

New-Item -ItemType Directory -Path $state, $reports -Force
```

Grant only the scheduled-task identity and operators who need the reports
access to these folders. Event history can contain account names, addresses,
and changed directory values.

### 2. Run an interactive readiness preflight

```powershell
$readiness = Test-EVXReadiness `
    -Type ActiveDirectoryChanges `
    -Collector WEC01 `
    -SubscriptionName EventViewerX-ADChanges `
    -ActiveDirectory CurrentDomain

$readiness.Checks |
    Format-Table Layer, Status, DiagnosticKind, EvidenceLevel, Target, Check -AutoSize

$readiness.RequiredFailures
$readiness.UnknownRequiredChecks
```

This preflight runs under the current elevated identity. It validates the
selected deployment before task registration, but it does not prove the
permissions of Local System, a gMSA, or another scheduled principal. Step 7
runs the same readiness assessment through the actual task principal and saves
its typed result for inspection.

Here `CurrentDomain` matches the Domain Controllers group SID used by the
subscription ACL and supplies that domain's expected DC set. On a local
collector, readiness compares the set with WEC runtime enrollment, so a DC that
never enrolled is visible instead of disappearing from the status. For a
multi-domain forest, build the allowed-source ACL from each intended domain's
Domain Controllers SID before assessing `CurrentForest`; use `-ExpectedSource`
when the intended sources do not match a directory discovery scope.

A successful original-channel query proves collector transport and event
presence. It does not prove each remote source's effective audit policy, so
those checks remain `Unknown`; inspect effective policy on the source computers
before accepting the deployment.

### 3. Install and verify the compiled CLI

The PowerShell Gallery module does not install the `evx` command. For
interactive use on a host with .NET 10, install the versioned .NET tool and
verify it before continuing:

```powershell
dotnet tool install --global EventViewerX.Cli --version 4.0.0
evx --version
```

A global tool is installed for the current user and may not be visible to a
scheduled task identity. For Task Scheduler, services, or hosts without .NET
10, deploy a release ZIP to an explicit machine path instead. Download the CLI
ZIP and `SHA256SUMS.txt` from the matching
[EventViewerX release](https://github.com/EvotecIT/EventViewerX/releases) before
registering a task. Choose `win-x64` for Intel/AMD Windows or `win-arm64` for
Windows on Arm. Choose `FrameworkDependent` when the .NET 10 runtime is
installed; choose `PortableCompat` when the task host needs the bundled
runtime.

After downloading exactly one matching CLI ZIP, verify and extract it from the
same elevated PowerShell session:

```powershell
$download = Join-Path $env:USERPROFILE 'Downloads'
$archives = @(Get-ChildItem -LiteralPath $download -Filter 'EventViewerX.Cli-*.zip')
if ($archives.Count -ne 1) {
    throw 'Keep exactly one selected EventViewerX CLI ZIP in the download folder.'
}

$archive = $archives[0]
$checksums = Join-Path $download 'SHA256SUMS.txt'
$checksumLine = @(Select-String `
    -LiteralPath $checksums `
    -Pattern ([regex]::Escape($archive.Name) + '$'))
if ($checksumLine.Count -ne 1) {
    throw "No unique checksum was found for $($archive.Name)."
}

$expectedHash = ($checksumLine[0].Line -split '\s+')[0]
$actualHash = (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) {
    throw "Checksum verification failed for $($archive.Name)."
}

$cliRoot = 'C:\Program Files\EventViewerX'
New-Item -ItemType Directory -Path $cliRoot -Force | Out-Null
Expand-Archive -LiteralPath $archive.FullName -DestinationPath $cliRoot -Force

$evx = Join-Path $cliRoot 'evx.exe'
if (-not (Test-Path -LiteralPath $evx -PathType Leaf)) {
    throw 'The verified CLI archive did not contain evx.exe at its release root.'
}
& $evx help
```

Keep the ZIP and checksum file together with the deployment record. Repeat the
verification when upgrading the scheduled host.

### 4. Test one checkpointed collection run

```powershell
& $evx query `
    --type ActiveDirectoryChanges `
    --collector WEC01 `
    --since 00:20:00 `
    --write-store C:\ProgramData\EventViewerX\events.db `
    --checkpoint EventViewerX-ADChanges
```

`--since` declares only the intentional first-run backfill window. The named
checkpoint is committed in the same SQLite transaction as the normalized
events. Later runs resume after the last successfully inspected
`ForwardedEvents` record, regardless of how long the scheduled task was
offline. EventViewerX probes the oldest and newest retained collector records
before and after each query and refuses to advance when a channel clear,
replacement, or retention gap makes completeness unknowable.

Treat the checkpoint name, collector spelling, collector channel, and event-type
selection as one stable collection identity. Use a new checkpoint name when
that identity changes, or reset the existing checkpoint intentionally after
reviewing the resulting backfill boundary.

If retention was exhausted, preserve the diagnostic evidence and decide what
period can be recovered before resetting only the collection checkpoint. This
does not delete retained EventViewerX rows:

```powershell
& $evx store reset-checkpoint `
    --path C:\ProgramData\EventViewerX\events.db `
    --consumer EventViewerX-ADChanges `
    --computer WEC01 `
    --container ForwardedEvents
```

The next run after a reset again uses `--since` as its declared initial
boundary. A reset cannot recreate events that have already aged out of
`ForwardedEvents`; treat that interval as incomplete.

The command prints inserted and duplicate counts plus the committed record
boundary to the task history stream. Run it twice to confirm that the second
run resumes at that boundary and does not create duplicate history.

### 5. Test the rolling daily report

```powershell
& $evx report `
    --store C:\ProgramData\EventViewerX\events.db `
    --type ActiveDirectoryChanges `
    --since 1.00:00:00 `
    --summary Day `
    --title 'Daily Active Directory changes' `
    --html C:\Reports\EventViewerX\AD-Changes-Daily.html `
    --excel C:\Reports\EventViewerX\AD-Changes-Daily.xlsx
```

`--since 1.00:00:00` is a rolling 24-hour window. The daily aggregation uses
UTC calendar buckets in the retained store and includes partial boundary days
when the rolling window crosses midnight.

### 6. Register the unattended tasks

The following example uses Local System on the WEC computer so no password is
stored in the task. If policy requires a gMSA or dedicated service account,
grant it only the channel, store, report-folder, and **Log on as a batch job**
rights it needs. Task Scheduler runs such identities in a non-interactive
session; do not use `-Open` or another UI-dependent action.

```powershell
Install-Module -Name PSEventViewer -Scope AllUsers -Force

$evx = 'C:\Program Files\EventViewerX\evx.exe'
$store = 'C:\ProgramData\EventViewerX\events.db'
$report = 'C:\Reports\EventViewerX\AD-Changes-Daily'
$readinessOutput = 'C:\ProgramData\EventViewerX\readiness.json'

$principal = New-ScheduledTaskPrincipal `
    -UserId 'SYSTEM' `
    -LogonType ServiceAccount `
    -RunLevel Highest

$readinessSettings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
$collectionSettings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
    -StartWhenAvailable
$reportSettings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 25) `
    -StartWhenAvailable

$readinessCommand = @'
Import-Module PSEventViewer
$readiness = Test-EVXReadiness `
    -Type ActiveDirectoryChanges `
    -Collector WEC01 `
    -SubscriptionName EventViewerX-ADChanges `
    -ActiveDirectory CurrentDomain
$readiness |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath C:\ProgramData\EventViewerX\readiness.json -Encoding UTF8
$collectorTarget = [EventViewerX.EventLogTarget]::LocalMachineName
$taskOwnedChecks = @($readiness.Checks | Where-Object {
    $_.Layer -eq [EventViewerX.EventReadinessLayer]::Runtime -or
    $_.Layer -eq [EventViewerX.EventReadinessLayer]::WindowsEventCollector -or
    ($_.Layer -eq [EventViewerX.EventReadinessLayer]::EventLogTransport -and
        ($_.Target -eq $collectorTarget -or
            $_.Target.StartsWith(
                $collectorTarget + '/',
                [StringComparison]::OrdinalIgnoreCase)))
})
$taskAccessFailures = @($taskOwnedChecks | Where-Object {
    $_.DiagnosticKind -eq [EventViewerX.EventReadinessDiagnosticKind]::AccessDenied
})
$taskRequiredFailures = @($taskOwnedChecks | Where-Object {
    $_.Required -and $_.Status -eq [EventViewerX.EventReadinessStatus]::Fail
})
$taskUnknownChecks = @($taskOwnedChecks | Where-Object {
    $_.Required -and $_.Status -eq [EventViewerX.EventReadinessStatus]::Unknown
})
if ($taskAccessFailures.Count -gt 0 -or
    $taskRequiredFailures.Count -gt 0 -or
    $taskUnknownChecks.Count -gt 0) {
    exit 5
}
'@
$readinessEncoded = [Convert]::ToBase64String(
    [Text.Encoding]::Unicode.GetBytes($readinessCommand))
$readinessAction = New-ScheduledTaskAction `
    -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -EncodedCommand $readinessEncoded"
Register-ScheduledTask `
    -TaskName 'EventViewerX-Verify-Readiness' `
    -Action $readinessAction `
    -Principal $principal `
    -Settings $readinessSettings

$collectAction = New-ScheduledTaskAction `
    -Execute $evx `
    -Argument "query --type ActiveDirectoryChanges --collector WEC01 --since 00:20:00 --write-store `"$store`" --checkpoint EventViewerX-ADChanges"
Register-ScheduledTask `
    -TaskName 'EventViewerX-Collect-ADChanges' `
    -Action $collectAction `
    -Principal $principal `
    -Settings $collectionSettings

$reportCommand = @'
$evx = 'C:\Program Files\EventViewerX\evx.exe'
$store = 'C:\ProgramData\EventViewerX\events.db'
$report = 'C:\Reports\EventViewerX\AD-Changes-Daily'
$collectionTaskName = 'EventViewerX-Collect-ADChanges'
$collectionWasRunning =
    (Get-ScheduledTask -TaskName $collectionTaskName).State -in 'Running', 'Queued'
$collectionRequestedAt = Get-Date
if (-not $collectionWasRunning) {
    Start-ScheduledTask -TaskName $collectionTaskName
}
$collectionDeadline = (Get-Date).AddMinutes(12)
do {
    Start-Sleep -Seconds 2
    $collectionTask = Get-ScheduledTask -TaskName $collectionTaskName
    $collectionInfo = Get-ScheduledTaskInfo -TaskName $collectionTaskName
    $collectionObserved = $collectionWasRunning -or
        $collectionInfo.LastRunTime -ge $collectionRequestedAt.AddSeconds(-2)
    if ((Get-Date) -ge $collectionDeadline) {
        exit 6
    }
} while ($collectionTask.State -in 'Running', 'Queued' -or -not $collectionObserved)
if ($collectionInfo.LastTaskResult -ne 0) {
    exit 6
}

$periodEnd = (Get-Date).ToUniversalTime().Date
$periodStart = $periodEnd.AddDays(-1)
& $evx report `
    --store $store `
    --type ActiveDirectoryChanges `
    --start $periodStart.ToString('o') `
    --end $periodEnd.ToString('o') `
    --summary Day `
    --title 'Daily Active Directory changes' `
    --html ($report + '.html') `
    --excel ($report + '.xlsx')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
'@
$reportEncoded = [Convert]::ToBase64String(
    [Text.Encoding]::Unicode.GetBytes($reportCommand))
$reportAction = New-ScheduledTaskAction `
    -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -EncodedCommand $reportEncoded"

Register-ScheduledTask `
    -TaskName 'EventViewerX-Report-ADChanges' `
    -Action $reportAction `
    -Principal $principal `
    -Settings $reportSettings
```

Windows grants and applies **Log on as a batch job** according to the task
principal and local/domain policy. Microsoft documents the right and its
security impact in [Log on as a batch job](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/security-policy-settings/log-on-as-a-batch-job).

### 7. Verify the unattended boundary

```powershell
Remove-Item -LiteralPath $readinessOutput -ErrorAction SilentlyContinue
$readinessRequestedAt = Get-Date
Start-ScheduledTask -TaskName 'EventViewerX-Verify-Readiness'
$readinessDeadline = (Get-Date).AddMinutes(12)
do {
    Start-Sleep -Seconds 2
    $readinessTask = Get-ScheduledTask -TaskName 'EventViewerX-Verify-Readiness'
    $readinessTaskInfo = Get-ScheduledTaskInfo -TaskName 'EventViewerX-Verify-Readiness'
    $readinessObserved =
        $readinessTaskInfo.LastRunTime -ge $readinessRequestedAt.AddSeconds(-2)
    if ((Get-Date) -ge $readinessDeadline) {
        throw 'The scheduled readiness assessment did not finish within 12 minutes.'
    }
} while ($readinessTask.State -in 'Running', 'Queued' -or -not $readinessObserved)

if ($readinessTaskInfo.LastTaskResult -ne 0) {
    throw "The scheduled readiness assessment failed with task result $($readinessTaskInfo.LastTaskResult)."
}
$taskReadiness = Get-Content -LiteralPath $readinessOutput -Raw |
    ConvertFrom-Json
$taskReadiness.Checks |
    Format-Table Layer, Status, DiagnosticKind, EvidenceLevel, Target, Check -AutoSize
Unregister-ScheduledTask -TaskName 'EventViewerX-Verify-Readiness' -Confirm:$false

$reportRequestedAt = Get-Date
Start-ScheduledTask -TaskName 'EventViewerX-Report-ADChanges'
$reportDeadline = (Get-Date).AddMinutes(30)
do {
    Start-Sleep -Seconds 2
    $reportTask = Get-ScheduledTask -TaskName 'EventViewerX-Report-ADChanges'
    $reportTaskInfo = Get-ScheduledTaskInfo -TaskName 'EventViewerX-Report-ADChanges'
    $reportObserved =
        $reportTaskInfo.LastRunTime -ge $reportRequestedAt.AddSeconds(-2)
    if ((Get-Date) -ge $reportDeadline) {
        throw 'The scheduled collection and report workflow did not finish within 30 minutes.'
    }
} while ($reportTask.State -in 'Running', 'Queued' -or -not $reportObserved)
if ($reportTaskInfo.LastTaskResult -ne 0) {
    throw "The scheduled report workflow failed with task result $($reportTaskInfo.LastTaskResult)."
}

$collectTaskInfo = Get-ScheduledTaskInfo -TaskName 'EventViewerX-Collect-ADChanges'
if ($collectTaskInfo.LastTaskResult -ne 0) {
    throw "The scheduled collection failed with task result $($collectTaskInfo.LastTaskResult)."
}

$outputs = Get-Item `
    C:\ProgramData\EventViewerX\events.db, `
    C:\Reports\EventViewerX\AD-Changes-Daily.html, `
    C:\Reports\EventViewerX\AD-Changes-Daily.xlsx
$outputs
$freshReportOutputs = @($outputs | Where-Object {
    $_.Extension -in '.html', '.xlsx' -and
    $_.LastWriteTimeUtc -ge $reportRequestedAt.ToUniversalTime().AddSeconds(-2)
})
if ($freshReportOutputs.Count -ne 2) {
    throw 'The scheduled report did not refresh both report outputs.'
}

$now = Get-Date
$collectTrigger = New-ScheduledTaskTrigger `
    -Once `
    -At $now.AddMinutes(2) `
    -RepetitionInterval (New-TimeSpan -Minutes 15)
$nextReport = $now.Date.AddHours(6).AddMinutes(5)
if ($nextReport -le $now) {
    $nextReport = $nextReport.AddDays(1)
}
$reportTrigger = New-ScheduledTaskTrigger -Daily -At $nextReport
Set-ScheduledTask `
    -TaskName 'EventViewerX-Collect-ADChanges' `
    -Trigger $collectTrigger
Set-ScheduledTask `
    -TaskName 'EventViewerX-Report-ADChanges' `
    -Trigger $reportTrigger
```

The one-time readiness task exits with code 5 when the scheduled identity
cannot prove its local runtime, collector, or collector-query boundary. Remote
source policy and channel permissions remain visible in the saved typed report
and require the separate source-side verification described above; they are not
permissions owned by the collector task identity.

The recurring tasks receive triggers only after that verification succeeds.
The collection task advances a durable collector-record checkpoint only in the
same transaction that stores a complete query result. The daily report task
starts or joins a collection run, waits for its successful completion, and then
reports the closed previous UTC day with explicit start and end boundaries.
This recovers retained backlog after task outages, avoids a rolling-window race,
and prevents report reads from overlapping the collection commit they depend
on.

The first scheduled report is complete only after collection has covered the
entire reported UTC day. Treat an earlier first report as onboarding proof, not
as a complete daily baseline.

Also verify WEC runtime status and the source forwarding operational log. A
successful task exit with an empty report can be correct when no matching
change occurred, but a readiness transport failure or stale source subscription
is not.

## Permission and firewall checklist

- [ ] The selected audit subcategories have the required effective outcomes.
- [ ] Selected directory objects have intentional SACLs for directory changes.
- [ ] Security, Application, System, and ForwardedEvents retain the desired
  monitoring window.
- [ ] The direct-query identity can read every selected source channel, or
  `NETWORK SERVICE` can read the source channels used by WEF.
- [ ] Direct collection has scoped Remote Event Log Management/RPC firewall
  access; WEF has scoped WinRM HTTP/HTTPS access.
- [ ] Collector subscription source ACLs contain only intended computer
  accounts or groups.
- [ ] The scheduled identity can read ForwardedEvents and write the store and
  report folders.
- [ ] A non-service scheduled identity has **Log on as a batch job** and no
  conflicting **Deny log on as a batch job** policy.
- [ ] Task timeouts, overlap, Event Log retention, store retention, and disk
  monitoring match the expected event volume.
- [ ] Readiness unknowns are accepted only with separate evidence; they are not
  silently treated as passes.

For direct RPC, Microsoft documents that the endpoint mapper uses TCP 135 and
then directs the client to a dynamically assigned RPC port in
[Configure firewall rules with Group Policy](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/configure).

## What EventViewerX intentionally does not do here

- It does not scan the rest of Active Directory or SYSVOL as a side effect of
  event readiness.
- It does not run user-supplied enrichment or callback code.
- It does not mutate audit policy, SACLs, firewall, channel security, or task
  credentials during assessment.
- It does not send notifications. Reports and transport-neutral email packages
  can be composed with Mailozaurr by users who need delivery today.
- It does not infer a remote effective audit policy from old events.

These boundaries keep EventViewerX focused on event collection, durable event
context, normalized reporting, and honest evidence.
