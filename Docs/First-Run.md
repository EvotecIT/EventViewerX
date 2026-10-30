# First run and a scheduled report

The module runs on 64-bit Windows PowerShell 5.1 or PowerShell 7.4 and later on
Windows. Its two module payloads target .NET Framework 4.7.2 and .NET 8. The
five NuGet libraries target net472, net8.0 and net10.0. The separate `evx` CLI
targets .NET 10.

Install the module for the account that will run it. These commands inspect
the local System log and write a report without opening a browser:

```powershell
Install-Module PSEventViewer -Scope CurrentUser
Import-Module PSEventViewer -ErrorAction Stop
Get-Module PSEventViewer | Select-Object Name, Version, Path

$reportDirectory = Join-Path $env:LOCALAPPDATA 'EventViewerX\Reports'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report = Show-EVXEvent -LogName System -EventId 7040 `
    -TimePeriod Last7Days -MaxEvents 500 `
    -HtmlPath (Join-Path $reportDirectory 'First-Run.html') `
    -CsvPath (Join-Path $reportDirectory 'First-Run.csv') -PassThru
$report.Rows | Select-Object -First 5 TimeCreated, SourceComputer, EventId
```

No rows means no matching records were found in the selected window. It does
not establish that the channel or audit policy is complete. Check the report's
coverage and run `Test-EVXReadiness` for the scenario you intend to monitor.

Use the same bounded query from another computer, a Windows Event Collector,
or a saved EVTX file. Run the remote and collector commands under an identity
that can read the selected channel; configure Windows Event Forwarding before
using `-Collector`.

```powershell
# Direct remote System log. Replace SERVER01 with the source computer.
Show-EVXEvent -LogName System -EventId 7040 -MachineName SERVER01 `
    -TimePeriod Last7Days -MaxEvents 500 `
    -HtmlPath (Join-Path $reportDirectory 'Remote.html')

# Forwarded security evidence. Replace WEC01 with the collector.
Test-EVXReadiness -Scenario AccountLockoutMonitoring -Collector WEC01
Show-EVXEvent -Type ADUserLockouts -Collector WEC01 `
    -TimePeriod Last7Days -MaxEvents 500 -MaxEventsScanned 50000 `
    -HtmlPath (Join-Path $reportDirectory 'Collector.html')

# Offline evidence. Replace the path with a saved .evtx file. The repository
# fixture below is a runnable example when this repository is checked out.
$evtxPath = Join-Path (Get-Location) 'Tests\Logs\NamedFilterExamples.evtx'
Show-EVXEvent -Path $evtxPath -EventId 7040 -MaxEvents 500 `
    -HtmlPath (Join-Path $reportDirectory 'Offline.html')
```

To report only membership changes involving selected groups and accounts,
build a typed predicate. The two conditions below must both match. Remove
one condition if only the group or only the member matters. This uses the
fields defined for `ADGroupMembershipChange`; it does not filter rendered
text after collection.

```powershell
$selection = New-EVXFilter -Type ADGroupMembershipChange
$selection.AllOf(
    $selection.Fields.GroupName.In('Domain Admins', 'Server Operators'),
    $selection.Fields.MemberName.MatchesWildcard('*svc-backup*'))
Show-EVXEvent -Type ADGroupMembershipChange -Where $selection.Predicate `
    -Collector WEC01 -TimePeriod Last24Hours -MaxEvents 10000 `
    -MaxEventsScanned 100000 `
    -HtmlPath (Join-Path $reportDirectory 'Selected-Membership.html')
```

## Schedule the same report

Save this script as `C:\ProgramData\EventViewerX\DailyReport.ps1`. Choose one
source mode. The script names each output with a UTC timestamp so a later run
does not overwrite the previous evidence. It produces HTML and CSV files; it
does not send mail or launch a browser.

```powershell
param(
    [ValidateSet('Local', 'Remote', 'Collector', 'Offline')]
    [string] $Mode = 'Local',
    [string] $ComputerName,
    [string] $EvtxPath,
    [string] $OutputDirectory = 'C:\ProgramData\EventViewerX\Reports'
)

$ErrorActionPreference = 'Stop'
Import-Module PSEventViewer -ErrorAction Stop
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$htmlPath = Join-Path $OutputDirectory "$Mode-$stamp.html"
$csvPath = Join-Path $OutputDirectory "$Mode-$stamp.csv"

switch ($Mode) {
    Local {
        $report = Show-EVXEvent -LogName System -EventId 7040 `
            -TimePeriod Last24Hours -MaxEvents 10000 `
            -HtmlPath $htmlPath -CsvPath $csvPath -PassThru
    }
    Remote {
        if (-not $ComputerName) { throw 'Remote mode requires -ComputerName.' }
        $report = Show-EVXEvent -LogName System -EventId 7040 `
            -MachineName $ComputerName -TimePeriod Last24Hours -MaxEvents 10000 `
            -HtmlPath $htmlPath -CsvPath $csvPath -PassThru
    }
    Collector {
        if (-not $ComputerName) { throw 'Collector mode requires -ComputerName.' }
        $report = Show-EVXEvent -Type ADUserLockouts -Collector $ComputerName `
            -TimePeriod Last24Hours -MaxEvents 10000 -MaxEventsScanned 100000 `
            -HtmlPath $htmlPath -CsvPath $csvPath -PassThru
    }
    Offline {
        if (-not $EvtxPath) { throw 'Offline mode requires -EvtxPath.' }
        $report = Show-EVXEvent -Path $EvtxPath -EventId 7040 -MaxEvents 10000 `
            -HtmlPath $htmlPath -CsvPath $csvPath -PassThru
    }
}

if (-not (Test-Path -LiteralPath $htmlPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $csvPath -PathType Leaf)) {
    throw 'The report did not produce both output files.'
}
$reportSnapshot = $report | Where-Object {
    $_ -is [EventViewerX.Reporting.EventReport]
} | Select-Object -First 1
if (-not $reportSnapshot) { throw 'The report snapshot is missing.' }
[pscustomobject]@{
    Mode = $Mode
    Rows = @($reportSnapshot.Rows).Count
    HtmlPath = $htmlPath
    CsvPath = $csvPath
}
```

Run the saved script interactively first. For an offline fixture from a source
checkout:

```powershell
$scriptPath = 'C:\ProgramData\EventViewerX\DailyReport.ps1'
$evtxPath = Join-Path (Get-Location) 'Tests\Logs\NamedFilterExamples.evtx'
& $scriptPath -Mode Offline -EvtxPath $evtxPath
```

For a local or offline task under Local System, register a daily task from an
elevated PowerShell session after saving the script and granting its output
directory appropriate access:

```powershell
Install-Module PSEventViewer -Scope AllUsers
$scriptPath = 'C:\ProgramData\EventViewerX\DailyReport.ps1'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -File `"$scriptPath`" -Mode Local"
$trigger = New-ScheduledTaskTrigger -Daily -At '06:00'
$principal = New-ScheduledTaskPrincipal -UserId 'NT AUTHORITY\SYSTEM' `
    -LogonType ServiceAccount
Register-ScheduledTask -TaskName 'EventViewerX Daily Report' `
    -Action $action -Trigger $trigger -Principal $principal
Start-ScheduledTask -TaskName 'EventViewerX Daily Report'
# After the task finishes, inspect its result and newest output.
Get-ScheduledTaskInfo -TaskName 'EventViewerX Daily Report' |
    Select-Object LastRunTime, LastTaskResult
Get-ChildItem 'C:\ProgramData\EventViewerX\Reports' -Filter 'Local-*.html' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 FullName, LastWriteTime
```

## Send the report with Mailozaurr

`evx report` and `evx watch` select a Mailozaurr profile by ID. The profile
holds provider settings and protected secrets; `--mail-to` is the report's
notification recipient policy. `--mail-dry-run` resolves the profile and
renders the email but never calls a provider. Remove it only when ready to
deliver. The interactive HTML report remains a separate artifact; recipients
get the compact HTML and plain-text email.

Install Mailozaurr for the account that creates the profile. The CLI uses the
same profile and secret stores under that account. For a scheduled task, create
the profile under the task identity or set `MAILOZAURR_PROFILE_DIRECTORY` and
`MAILOZAURR_SECRET_DIRECTORY` to protected directories accessible to it.

### Graph with an app registration and certificate

In Microsoft Entra, register a single-tenant application, add Microsoft Graph
application permission `Mail.Send`, grant admin consent, and upload the public
certificate (.cer) under Certificates & secrets. Keep the matching private
certificate (.pfx) on the reporting host, readable by the scheduled identity.
The mailbox in `-DefaultMailbox` is the sending mailbox. Scope the application's
mailbox access as required by your tenant policy; see [Exchange application
RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac).

```powershell
Install-Module Mailozaurr -MinimumVersion 3.0.5 -Scope CurrentUser
Import-Module Mailozaurr -MinimumVersion 3.0.5 -ErrorAction Stop
$graphSettings = @{
    clientId = '<application-client-id>'
    tenantId = '<tenant-id>'
    certificatePath = 'C:\ProgramData\EventViewerX\Secrets\mail-app.pfx'
}
New-MailProfile -ProfileId evx-graph -DisplayName 'Event reports via Graph' `
    -Kind Graph -DefaultSender 'events@contoso.com' `
    -DefaultMailbox 'events@contoso.com' -Settings $graphSettings
# If the PFX has a password, store it through Mailozaurr:
Set-MailProfileSecret -ProfileId evx-graph -Name certificatePassword `
    -Value (Read-Host 'Certificate password' -AsSecureString)
Test-MailProfile -ProfileId evx-graph

evx report --log System --event-id 7040 --since 7.00:00:00 `
    --email-html .\System-Email.html --mail-profile evx-graph `
    --mail-to operations@contoso.com --mail-dry-run
```

### SMTP relay

Use a relay that accepts the reporting host without basic authentication.
This example requests STARTTLS. Change the server, port, sender, and recipients
to match your relay's policy. Mailozaurr also supports authenticated SMTP
through its own profile secret service when that transport is appropriate.

```powershell
$smtpSettings = @{
    server = 'relay.contoso.com'
    port = '587'
    secureSocketOptions = 'StartTls'
    authenticationEnabled = 'false'
}
New-MailProfile -ProfileId evx-relay -DisplayName 'Event reports via relay' `
    -Kind Smtp -DefaultSender 'events@contoso.com' -Settings $smtpSettings
Test-MailProfile -ProfileId evx-relay

evx report --log System --event-id 7040 --since 7.00:00:00 `
    --email-html .\System-Email.html --mail-profile evx-relay `
    --mail-to operations@contoso.com --mail-dry-run
```

Mailozaurr's Gmail, SendGrid, Mailgun, and SES profile kinds use the same
`--mail-profile` and `--mail-to` options. A Mailozaurr `profiles.json` store
can be selected explicitly with `--mail-profile-store FILE`. No EventViewerX
mail-profile JSON format is used.

For a PowerShell pipeline, `Show-EVXEvent -EmailPackage` returns the same
transport-neutral body. The [PowerShell guide](PowerShell-Guide.md#create-html-excel-and-email-output)
shows how to hand it to Mailozaurr's profile send service.

For remote or collector mode, use a scheduled identity with network access and
the required Event Log permissions. Change the action's `-Mode` and add
`-ComputerName SERVER01` or `-ComputerName WEC01`; register the task under that
identity. Do not interpret an empty report as complete collection. Review the
readiness result, report coverage, task result, and output files under that
same identity before depending on the schedule.
