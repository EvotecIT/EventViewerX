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
| Audit User Account Management | Success | Domain controllers |
| Audit Computer Account Management | Success | Domain controllers |
| Audit Security Group Management | Success | Domain controllers |
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

The PowerShell Gallery module does not install `evx.exe`. Download the CLI ZIP
and `EventViewerX.Cli-SHA256SUMS.txt` from the matching
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
$checksums = Join-Path $download 'EventViewerX.Cli-SHA256SUMS.txt'
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

### 4. Test one overlapping collection run

```powershell
& $evx query `
    --type ActiveDirectoryChanges `
    --collector WEC01 `
    --since 00:20:00 `
    --write-store C:\ProgramData\EventViewerX\events.db
```

The command prints inserted and duplicate counts to the task history stream.
Run it twice to confirm that the second run reports duplicates instead of
creating duplicate history.

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

$settings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
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
$identityAccessFailures = $readiness.Checks | Where-Object {
    $_.DiagnosticKind -eq [EventViewerX.EventReadinessDiagnosticKind]::AccessDenied
}
if ($identityAccessFailures) {
    exit 5
}
'@
$readinessEncoded = [Convert]::ToBase64String(
    [Text.Encoding]::Unicode.GetBytes($readinessCommand))
$readinessAction = New-ScheduledTaskAction `
    -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -EncodedCommand $readinessEncoded"
$readinessTrigger = New-ScheduledTaskTrigger `
    -Once `
    -At (Get-Date).AddMinutes(5)

Register-ScheduledTask `
    -TaskName 'EventViewerX-Verify-Readiness' `
    -Action $readinessAction `
    -Trigger $readinessTrigger `
    -Principal $principal `
    -Settings $settings

$collectAction = New-ScheduledTaskAction `
    -Execute $evx `
    -Argument "query --type ActiveDirectoryChanges --collector WEC01 --since 00:20:00 --write-store `"$store`""
$collectTrigger = New-ScheduledTaskTrigger `
    -Once `
    -At (Get-Date).Date.AddMinutes(5) `
    -RepetitionInterval (New-TimeSpan -Minutes 15)

Register-ScheduledTask `
    -TaskName 'EventViewerX-Collect-ADChanges' `
    -Action $collectAction `
    -Trigger $collectTrigger `
    -Principal $principal `
    -Settings $settings

$reportAction = New-ScheduledTaskAction `
    -Execute $evx `
    -Argument "report --store `"$store`" --type ActiveDirectoryChanges --since 1.00:00:00 --summary Day --title `"Daily Active Directory changes`" --html `"$report.html`" --excel `"$report.xlsx`""
$reportTrigger = New-ScheduledTaskTrigger -Daily -At '06:05'

Register-ScheduledTask `
    -TaskName 'EventViewerX-Report-ADChanges' `
    -Action $reportAction `
    -Trigger $reportTrigger `
    -Principal $principal `
    -Settings $settings
```

Windows grants and applies **Log on as a batch job** according to the task
principal and local/domain policy. Microsoft documents the right and its
security impact in [Log on as a batch job](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/security-policy-settings/log-on-as-a-batch-job).

### 7. Verify the unattended boundary

```powershell
Remove-Item -LiteralPath $readinessOutput -ErrorAction SilentlyContinue
Start-ScheduledTask -TaskName 'EventViewerX-Verify-Readiness'
$readinessDeadline = (Get-Date).AddMinutes(2)
do {
    Start-Sleep -Seconds 2
    $readinessTask = Get-ScheduledTask -TaskName 'EventViewerX-Verify-Readiness'
    if ((Get-Date) -ge $readinessDeadline) {
        throw 'The scheduled readiness assessment did not finish within two minutes.'
    }
} while ($readinessTask.State -eq 'Running')

$readinessTaskInfo = Get-ScheduledTaskInfo -TaskName 'EventViewerX-Verify-Readiness'
if ($readinessTaskInfo.LastTaskResult -ne 0) {
    throw "The scheduled readiness assessment failed with task result $($readinessTaskInfo.LastTaskResult)."
}
$taskReadiness = Get-Content -LiteralPath $readinessOutput -Raw |
    ConvertFrom-Json
$taskReadiness.Checks |
    Format-Table Layer, Status, DiagnosticKind, EvidenceLevel, Target, Check -AutoSize
Unregister-ScheduledTask -TaskName 'EventViewerX-Verify-Readiness' -Confirm:$false

Start-ScheduledTask -TaskName 'EventViewerX-Collect-ADChanges'
Start-ScheduledTask -TaskName 'EventViewerX-Report-ADChanges'

Get-ScheduledTaskInfo -TaskName 'EventViewerX-Collect-ADChanges'
Get-ScheduledTaskInfo -TaskName 'EventViewerX-Report-ADChanges'

Get-Item `
    C:\ProgramData\EventViewerX\events.db, `
    C:\Reports\EventViewerX\AD-Changes-Daily.html, `
    C:\Reports\EventViewerX\AD-Changes-Daily.xlsx
```

The one-time readiness task exits with code 5 when the scheduled identity sees
an `AccessDenied` diagnostic. Other `Unknown` checks, such as effective policy
that can only be inspected on a remote source, remain visible in the saved
typed report and require separate evidence rather than being treated as pass.

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
