# PSEventViewer PowerShell guide

This guide is organized by task. All examples work in Windows PowerShell 5.1
and PowerShell 7 unless a Windows permission or feature is explicitly required.

```powershell
Install-Module -Name PSEventViewer -Scope CurrentUser
Import-Module PSEventViewer
```

## Read events

### Local channels

Use native filters whenever possible. They reduce the records Windows must
return and the work PowerShell must perform.

```powershell
Get-EVXEvent `
    -LogName Security `
    -EventId 4624, 4625 `
    -StartTime (Get-Date).AddHours(-1) `
    -ReadMode Metadata `
    -MaxEvents 1000
```

Provider-only discovery finds the channels linked to that provider:

```powershell
Get-EVXEvent `
    -ProviderName Microsoft-Windows-Kernel-General `
    -EventId 12 `
    -MaxEvents 10
```

`New-EVXFilter` creates a reusable `EventViewerX.EventFilter` by default. The
same object can be passed to query, export, watcher, and collector commands.
Use `-AsXPath`, `-LogName`, or `-Path` only when another Windows tool needs
native query text.

```powershell
$filter = New-EVXFilter `
    -EventId 4624, 4625 `
    -StartTime (Get-Date).AddHours(-1) `
    -Level Error, Warning

Get-EVXEvent -LogName Security -Filter $filter -ReadMode Metadata
```

Named-data exclusion uses a native `Suppress` clause so events without the
named field remain in the result:

```powershell
$queryXml = New-EVXFilter `
    -LogName Security `
    -EventId 4624, 4625 `
    -NamedDataExcludeFilter @{
        TargetUserName = 'svc-noisy'
    }

Get-EVXEvent -FilterXml $queryXml -ReadMode StructuredData
```

Do not combine `-NamedDataExcludeFilter` with `-AsXPath`. The Windows Event
Log XPath subset cannot safely express “exclude this value but keep events
where the field is absent,” so the command fails explicitly instead.

### Get-WinEvent-compatible filter hashtables

The familiar keys `LogName`, `ProviderName`, `Path`, `Id`, `Level`,
`Keywords`, `StartTime`, `EndTime`, `UserId`, and `Data` are supported. Extra
keys are treated as named `EventData` fields.

```powershell
Get-EVXEvent -FilterHashtable @{
    LogName       = 'Security'
    Id            = 4625
    StartTime     = (Get-Date).AddHours(-1)
    TargetUserName = 'alice'
} -MaxEvents 100
```

Use `SuppressHashFilter` when a broad native selection needs native
suppressions:

```powershell
Get-EVXEvent -FilterHashtable @{
    LogName           = 'System'
    StartTime         = (Get-Date).AddDays(-1)
    SuppressHashFilter = @{
        Id = 7036
    }
} -MaxEvents 500
```

Offline provider wildcards that must be expanded before a structured
Select/Suppress query are discovered with metadata-only reads. Discovery is
cancellable and stops at `-MaxEventsScanned`, or at 65,536 records when no
explicit scan bound is supplied. If the file is larger, the command fails
instead of silently compiling an incomplete provider list. Use exact provider
names for the fastest large-file path, or raise `-MaxEventsScanned`
deliberately.

### Discoverable typed predicates

Hashtable filters are useful for native event metadata, but typed reports have
domain fields. Ask the definition for those fields instead of guessing XML
names:

```powershell
$filter = New-EVXFilter -Type ADUserLogonFailed
$filter.Fields | Get-Member -MemberType Property
$filter.Fields.Who |
    Select-Object Name, Description, ValueType, FilterStage, SupportedOperators

$filter.AllOf(
    $filter.Fields.Who.MatchesWildcard('CONTOSO\*'),
    $filter.Fields.IpAddress.NotIn('-', '::1'))

Get-EVXEvent -Filter $filter -TimePeriod Last24Hours
```

For an interactive expression, use the restricted script-block form:

```powershell
Get-EVXEvent -Type ADUserLogonFailed `
    -Where { $_.Who -like 'CONTOSO\*' -and $_.IpAddress -notin @('-', '::1') }

Get-EVXEvent -Type ADUserLogonFailed `
    -Where { $_.Who -like 'CONTOSO\*' } -Explain

Get-EVXEvent -Type ADUserLogonFailed -Describe

New-EVXFilter -Type ADUserLogonFailed `
    -Where { $_.Who -like 'CONTOSO\*' } -Explain
```

The expression is parsed and never invoked. It supports comparison,
membership, wildcard, regex, collection, and Boolean operators but rejects
commands and unrelated variables. `-Explain` shows which comparisons Windows
can prefilter and which require exact managed verification.

Predicates serialize to the same enum-named JSON used by `evx --where`. Let
the builder validate fields and values before saving the file:

```powershell
$filter = New-EVXFilter -Type ADUserLogonFailed
$filter.Fields.Who.MatchesWildcard('CONTOSO\*').ToJson() |
    Set-Content -LiteralPath .\failed-logons.filter.json -Encoding utf8

evx query --type ADUserLogonFailed `
    --where .\failed-logons.filter.json --explain
```

### Structured QueryList XML

Use `-FilterXml` for several paths, explicit Select/Suppress combinations, or
an existing Windows Event Viewer custom view.

```powershell
$query = @'
<QueryList>
  <Query Id="0">
    <Select Path="System">*[System[(Level=1 or Level=2)]]</Select>
    <Suppress Path="System">*[System[EventID=10016]]</Suppress>
  </Query>
</QueryList>
'@

Get-EVXEvent -FilterXml $query -ReadMode Message -MaxEvents 100
```

### Resume from a bookmark

Bookmark creation is opt-in so ordinary large scans do not pay for it. Save
the event's `BookmarkXml` string rather than the framework-specific bookmark
object; the string works in Windows PowerShell 5.1 and PowerShell 7.

```powershell
$last = Get-EVXEvent `
    -LogName System `
    -ReadMode Metadata `
    -IncludeBookmark `
    -MaxEvents 1

$last.BookmarkXml | Set-Content -LiteralPath .\system.bookmark.xml

Get-EVXEvent `
    -LogName System `
    -ReadMode Metadata `
    -BookmarkXml (Get-Content -LiteralPath .\system.bookmark.xml -Raw)
```

The default bookmark offset resumes after the saved event. Use
`-BookmarkOffset 0` when the saved event itself must be returned again.

### Offline EVTX files

```powershell
Get-EVXEvent `
    -Path C:\Logs\Security.evtx `
    -Oldest `
    -ReadMode StructuredData `
    -NamedDataFilter @{ TargetUserName = 'alice' } `
    -MaxEvents 100
```

Several files can be read as one deterministic stream:

```powershell
Get-EVXEvent `
    -Path C:\Logs\DC01-Security.evtx, C:\Logs\DC02-Security.evtx `
    -Oldest `
    -ReadMode Metadata `
    -MaxConcurrency 2
```

### Remote computers

```powershell
Get-EVXEvent `
    -LogName Security `
    -MachineName DC01, DC02 `
    -EventId 4740 `
    -MaxConcurrency 4 `
    -ContinueOnError `
    -MaxEvents 500
```

`-ContinueOnError` isolates a failed source so healthy hosts can continue.
Use `-Credential`, `-Authentication`, `-RemoteConnectionTimeoutMs`, and
`-RemoteReadTimeoutMs` when the default Windows Event Log session is not
appropriate. Results retain both the queried source and the computer recorded
inside the event.
When selecting an explicit authentication package for catalog or administration
commands, also provide `-Credential`; the managed Windows catalog API cannot
enforce Kerberos, NTLM, or Negotiate while using its current-identity overload.

### Typed event definitions

Built-in types turn common event families into useful typed objects. A type
owns its source logs, providers, event IDs, filters, and projection, so do not
combine `-Type` with `-LogName`. Use `-LogName` for generic queries. An offline
`-Path` can be combined with `-Type` because the file supplies the container
while the type still supplies the event semantics.

```powershell
Get-EVXEvent `
    -Type ADUserLogonFailed, ADUserLockouts `
    -MachineName DC01, DC02 `
    -TimePeriod Last24Hours `
    -MaxEvents 500 |
    Select-Object TimeCreated, TypeName, MachineName, UserName, IpAddress
```

DNS enrichment is opt-in and bounded:

```powershell
Get-EVXEvent `
    -Type ADSMBServerAuditV1 `
    -MachineName DC01, DC02 `
    -TimePeriod Last3Days `
    -ResolveDns `
    -DnsTimeoutMs 1000 `
    -DnsMaxConcurrency 8
```

EventViewerX uses DnsClientX for this optional layer because reporting and
multi-host automation need explicit request timeouts, bounded concurrency,
retry classification, stable result status, and testable resolver behavior.
`System.Net.Dns` remains appropriate for simple one-off lookups, but it does
not provide that complete operational contract. DNS failure annotates the
typed event and never removes it from the result stream.

See [`Query-EventTypes.ps1`](../Examples/Query-EventTypes.ps1).

Use `-Definition` when the desired type is not built in. The same JSON can be
queried, reported, watched, or compiled into a WEC subscription without adding
another command:

```powershell
Get-EVXEvent -Definition .\ServiceChanges.json `
    -MachineName SRV01, SRV02 -TimePeriod Last24Hours

Start-EVXWatcher -Definition .\ServiceChanges.json `
    -Action { $_ | ConvertTo-Json -Depth 5 }
```

See [custom event definitions](Event-Definitions.md) and
[`Query-CustomDefinition.ps1`](../Examples/Query-CustomDefinition.ps1).

### Create HTML, Excel, and email output

`Show-EVXEvent` is the one report command. It queries once and renders each
selected output from the same normalized snapshot.

`-Type` and `-Definition` create domain tables. Each populated leaf type has
its own columns and, in Excel, its own worksheet. A composite type therefore
keeps logons, group-policy changes, Kerberos activity, and other incompatible
schemas separate. The primary typed tables do not add Event ID, provider, log
name, or raw message columns. Excel records those technical fields in the
separate `Event Provenance` worksheet. `-LogName` is the generic path and keeps
the familiar Windows event metadata for browsing an arbitrary channel.

```powershell
Show-EVXEvent -Type ActiveDirectoryAuthentication `
    -Collector WEC01 -TimePeriod Last24Hours `
    -HtmlPath .\Authentication.html `
    -ExcelPath .\Authentication.xlsx `
    -EmailPackage -PassThru

Get-EVXEvent -LogName System -EventId 41, 6008 -TimePeriod Last7Days |
    Show-EVXEvent -HtmlPath .\Startup.html
```

With no explicit output and no `-PassThru`, the command creates and opens a
temporary HTML report. `-EmailPackage` returns transport-neutral HTML, plain
text, inline resources, and attachments for Mailozaurr, Graph, or Teams
adapters; PSEventViewer itself does not send or own credentials. Its compact
digest distributes the row limit across populated typed sections instead of
letting the first event type consume the entire email.

```powershell
$email = Show-EVXEvent -Type ADUserLogonFailed `
    -TimePeriod Last24Hours -EmailPackage

Send-EmailMessage -Server 'smtp.contoso.com' -Port 587 `
    -From 'events@contoso.com' -To 'operations@contoso.com' `
    -Subject $email.Subject -HTML $email.Html -Text $email.PlainText `
    -Credential $credential -SecureSocketOptions StartTls
```

`Send-EmailMessage` is supplied by Mailozaurr. Keeping it outside the module
lets the same package work with SMTP, Microsoft Graph, or a future transport
adapter without making any sender a PSEventViewer dependency.

### Retain and summarize typed history

Use `-StorePath` on the same `Show-EVXEvent` query that creates a report. The
normalized rows, homogeneous report schemas, and event provenance are written
transactionally through the optional DbaClientX-backed SQLite store:

```powershell
$store = 'C:\ProgramData\EventViewerX\events.db'

Show-EVXEvent `
    -Type ActiveDirectoryAuthentication `
    -Collector WEC01 `
    -TimePeriod Last15Minutes `
    -StorePath $store
```

Read retained history with the same typed properties and selectors. A
composite type expands to its stored leaf definitions; it is not treated as a
literal report-table name:

```powershell
$filter = New-EVXFilter -Type ADUserLogonFailed

Show-EVXEvent `
    -FromStore $store `
    -Type ADUserLogonFailed `
    -Where $filter.Fields.Who.MatchesWildcard('CONTOSO\*') `
    -StartTime (Get-Date).AddDays(-7) `
    -HtmlPath .\FailedLogons.html `
    -ExcelPath .\FailedLogons.xlsx `
    -CsvPath .\FailedLogons.csv

Show-EVXEvent `
    -FromStore $store `
    -Type ActiveDirectoryAuthentication `
    -StartTime (Get-Date).AddMonths(-1) `
    -SummaryPeriod Day `
    -HtmlPath .\Authentication-Daily.html
```

Repeated collection is safe. Original source provenance deduplicates direct
and ForwardedEvents copies of the same event. Retention remains explicit
through `evx store prune`; EventViewerX does not silently delete history.

## Choose a read mode

Select the least expensive mode that contains the fields the next step needs.

| Mode | Materialized data | Typical use |
| --- | --- | --- |
| `Metadata` | System fields only | Counts, IDs, timestamps, provider, record ID, large scans |
| `Message` | Metadata plus formatted provider message and labels | Human-readable reports and alerts |
| `StructuredData` | Metadata, typed payload, named data, and XML; no message formatting | Analysis and structured export |
| `RawXml` | Native event XML | Lowest-overhead, byte-stable interchange |
| `Full` | Message and complete structured projection | Interactive investigation |

Messages can be deterministic instead of following the machine locale:

```powershell
Get-EVXEvent `
    -LogName System `
    -ReadMode Message `
    -MessageCulture en-US `
    -FallbackMessageCulture de-DE `
    -MaxEvents 100
```

The result exposes render status. A missing provider resource is distinguishable
from an event whose message is genuinely empty.

## Process large logs

### Stream instead of accumulating

`Get-EVXEvent` writes records as they are detached from Windows. Process the
pipeline incrementally and avoid wrapping the complete command in `@(...)`
unless all records really must be retained.

```powershell
Get-EVXEvent `
    -Path C:\Logs\Security.evtx `
    -Oldest `
    -ReadMode Metadata |
    ForEach-Object {
        # Process one record. Keep this block small.
        if ($_.Id -eq 4625) {
            $_
        }
    }
```

For a custom aggregation, keep only the state you need rather than retaining
event objects:

```powershell
$counts = @{}
Get-EVXEvent -LogName Security -ReadMode Metadata -MaxEvents 100000 |
    ForEach-Object {
        $key = '{0}/{1}' -f $_.ProviderName, $_.Id
        $counts[$key] = 1 + [int] $counts[$key]
    }
$counts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 20
```

### Export directly

`Export-EVXEvent` keeps parsing, projection, serialization, buffering, and the
atomic destination-file replacement in compiled code.

```powershell
# Fastest byte-stable interchange.
Export-EVXEvent `
    -Path C:\Logs\Security.evtx `
    -OutputPath C:\Exports\Security.xml `
    -Format Xml `
    -Oldest `
    -Force

# Complete structured records plus deterministic English messages.
Export-EVXEvent `
    -Path C:\Logs\Security.evtx `
    -OutputPath C:\Exports\Security.jsonl `
    -Format JsonLines `
    -ReadMode Full `
    -MessageCulture en-US `
    -Oldest `
    -Force

# Bounded remote export written on the calling computer.
Export-EVXEvent `
    -LogName System `
    -MachineName DC01 `
    -OutputPath C:\Exports\DC01-System.csv `
    -Format Csv `
    -ReadMode Message `
    -BufferCapacity 64 `
    -Force
```

CSV and JSON Lines are projections, so their schemas and byte counts are not
equivalent to another tool unless the selected fields are identical. XML
comparison can use the exact-output contract documented in the
[benchmark guide](../Benchmarks/EventLogParsing/README.md).

Native EVTX export is local-only because Windows creates the file in the target
session. `-ArchiveResources` can retain provider resources needed to render the
archive later.

See [`Export-LargeLog.ps1`](../Examples/Export-LargeLog.ps1).

## Resume and watch

### Durable polling checkpoints

Use a checkpoint when a scheduled task polls a channel repeatedly:

```powershell
Get-EVXEvent `
    -LogName Security `
    -EventId 4625 `
    -RecordIdFile C:\State\FailedLogons.json `
    -RecordIdKey FailedLogons `
    -ReadMode StructuredData
```

Progress is tied to the source and log generation. The engine handles
boundaries without replaying the last accepted event. Reset intentionally:

```powershell
Reset-EVXEventCheckpoint `
    -Path C:\State\FailedLogons.json `
    -Confirm:$false
```

Structured queries can share one checkpoint file without mixing channel record
sequences. Each `Path` in the QueryList receives its own checkpoint entry:

```powershell
[xml] $query = @'
<QueryList>
  <Query Id="0" Path="System">
    <Select Path="System">*[System[Level &lt;= 2]]</Select>
  </Query>
  <Query Id="1" Path="Application">
    <Select Path="Application">*[System[Level &lt;= 2]]</Select>
  </Query>
</QueryList>
'@

Get-EVXEvent `
    -FilterXml $query `
    -RecordIdFile C:\State\CriticalEvents.json `
    -RecordIdKey CriticalEvents `
    -ReadMode StructuredData
```

Reset that workflow with the same key. The reset starts a new generation for
the base key and every existing per-source entry derived from it:

```powershell
Reset-EVXEventCheckpoint `
    -Path C:\State\CriticalEvents.json `
    -RecordIdKey CriticalEvents `
    -Confirm:$false
```

Use bookmarks when another component owns native bookmark XML. Use checkpoints
for a file-backed polling workflow owned by PSEventViewer.

### Real-time watchers

```powershell
$watcher = Start-EVXWatcher `
    -Name FailedLogons `
    -LogName Security `
    -EventId 4625 `
    -Start Future `
    -ReadMode Full `
    -StopAfter 10 `
    -TimeOut (New-TimeSpan -Minutes 30) `
    -Action {
        param($Event)
        $Event | Select-Object TimeCreated, Id, MachineName, Data
    }

Get-EVXWatcher -Id $watcher.Id
Stop-EVXWatcher -Id $watcher.Id -Confirm:$false
```

Watcher buffers are bounded. Keep the action fast; hand expensive downstream
work to another bounded queue when event arrival can be sustained.

See [`Watch-Events.ps1`](../Examples/Watch-Events.ps1).

## Inspect and administer Windows Event Log

### Providers, channels, and health

```powershell
Get-EVXLog -LogName 'Microsoft-Windows-PowerShell/*' -Force |
    Select-Object LogName, IsEnabled, LogMode, MaximumSizeInBytes

Get-EVXProvider `
    -Name Microsoft-Windows-PowerShell `
    -IncludeEvents

Test-EVXLog -LogName System -MaxEventsToScan 10
```

`Get-EVXProvider -NameOnly` avoids materializing provider metadata when only
discovery is needed. `-IncludeEvents` is the expensive, explicit template
inventory. See [`Inspect-Catalog.ps1`](../Examples/Inspect-Catalog.ps1).

### Manifest channel policy

```powershell
Set-EVXLog `
    -LogName Microsoft-Windows-PowerShell/Operational `
    -Enabled $true `
    -MaximumSizeMB 64 `
    -Mode Circular
```

`Update-EVXLogArchive` archives provider resources beside an EVTX file so
historical messages can be rendered on another machine.

### Classic logs and sources

```powershell
New-EVXLog `
    -LogName Contoso-App `
    -ProviderName Contoso-App-Source `
    -MaximumKilobytes 20480 `
    -OverflowAction OverwriteAsNeeded

New-EVXSource -LogName Contoso-App -SourceName Contoso-Worker

Clear-EVXLog `
    -LogName Contoso-App `
    -BackupPath C:\EventBackups\Contoso-App.evtx `
    -Confirm:$false

Remove-EVXSource `
    -SourceName Contoso-Worker `
    -Confirm:$false

Remove-EVXLog -LogName Contoso-App -Confirm:$false
```

Creating/removing sources and logs changes machine-wide registry state and
normally requires elevation. See [`Manage-Logs.ps1`](../Examples/Manage-Logs.ps1).

### Windows Event Collector

```powershell
# One-time collector setup and a read-only readiness check use the existing
# Set/Get cmdlets rather than adding WEC-specific command sprawl.
Set-EVXCollectorSubscription -InitializeCollector -Confirm:$false
Get-EVXCollectorSubscription -Readiness

# Source-initiated forwarding scales without maintaining a source list in WEC.
$domainControllersSid = (Get-ADGroup 'Domain Controllers').SID.Value
$definition = New-EVXCollectorSubscription `
    -Name 'Domain controller authentication' `
    -SubscriptionType SourceInitiated `
    -CollectorHostName WEC01.ad.contoso.com `
    -AllowedSourceSid $domainControllersSid `
    -Type ActiveDirectoryAuthentication `
    -Description 'Typed authentication events from domain controllers'

# Apply the returned value through the Windows Event Forwarding
# SubscriptionManager computer policy on the source domain controllers.
$definition.SourceSubscriptionManagerValue
$definition | Set-EVXCollectorSubscription `
    -InitializeCollector -Confirm:$false

Get-EVXCollectorSubscription `
    -Name $definition.SubscriptionId `
    -IncludeRuntimeStatus |
    Select-Object SubscriptionName, Enabled, RuntimeStatus

Set-EVXCollectorSubscription `
    -Name 'Domain controller authentication' `
    -Enabled $true `
    -Confirm:$false

Set-EVXCollectorSubscription `
    -Name 'Domain controller authentication' -Remove -Confirm:$false
```

Inventory can target a remote collector. Updates are intentionally local-only
because the Windows Event Collector write API has no remote-session contract.
`-IncludeRuntimeStatus` is also local-only because the WEC runtime API does not
accept a remote session; use PowerShell remoting to execute it on another
collector.

For source-initiated forwarding, deploy
`$definition.SourceSubscriptionManagerValue` through **Computer Configuration >
Administrative Templates > Windows Components > Event Forwarding > Configure
target Subscription Manager**. The module generates the exact HTTP/HTTPS URI
and refresh interval. The source authorization SDDL is generated from
`-AllowedSourceSid`, so a caller does not need to author SDDL by hand.

Security events are forwarded by Network Service. Preserve each source's
existing Security channel descriptor and grant Network Service read access if
it is missing. Domain controllers require their Domain Controllers group SID
(RID 516) or explicit computer-account SIDs; the inbox Domain Computers ACE
does not authorize domain controllers. `RuntimeStatus.Sources` then provides
per-source Active/Trying state, processed-event counts, heartbeat time, and the
native Windows error code. Do not treat a configured subscription as proven
until sources are Active and events are arriving in `ForwardedEvents`.
Affected Windows Server 2025 builds can terminate the Event Log service when
`ForwardedEvents` evaluates any filtered native XPath, not only a
`TimeCreated` predicate. `Get-EVXEvent -Collector` therefore opens that channel
once with `*` and applies the complete event ID, provider, original-channel,
data, checkpoint, and time selection in its bounded ordered reader. Direct live
logs and EVTX files retain their selective native-query fast path. Raw filtered
`-FilterXPath` and structured `QueryList` input for `ForwardedEvents` are
rejected before Windows can execute them; use the normal typed/filter
parameters and bound wide collector scans with checkpoints, `-MaxEvents`, or
`-MaxEventsScanned`.
See [`Manage-Collector.ps1`](../Examples/Manage-Collector.ps1).

## Recover PowerShell script blocks

Reconstruct script block fragments from Windows PowerShell or PowerShell
operational events:

```powershell
Get-EVXPowerShellScript `
    -Edition WindowsPowerShell `
    -MachineName DC01, DC02 `
    -OutputPath C:\RecoveredScripts `
    -MaxScripts 100 `
    -MaxEventsScanned 50000 `
    -MaxPendingScripts 512 `
    -MaxCachedEvents 2048 `
    -IncludeQueryInfo
```

Use the `Execution` parameter set when execution context and event sequence are
needed rather than reconstructed source files:

```powershell
Get-EVXPowerShellScript `
    -Execution `
    -Edition WindowsPowerShell `
    -MachineName DC01 `
    -MaxEvents 100 `
    -MaxEventsScanned 50000
```

Use `-EventLogPath` without `-MachineName` to recover from one local exported
operational log. The file is queried exactly once:

```powershell
Get-EVXPowerShellScript `
    -Edition WindowsPowerShell `
    -EventLogPath C:\Logs\WindowsPowerShell-Operational.evtx `
    -OutputPath C:\RecoveredScripts `
    -MaxScripts 100
```

The scan limits are intentional resource bounds, not result counts. The engine
uses a one-record lookahead so hitting a limit is reported as truncation only
when another matching record actually exists. See
[`Restore-PowerShellScripts.ps1`](../Examples/Restore-PowerShellScripts.ps1).

## Write events

### Classic Event Log

```powershell
Write-EVXEvent `
    -LogName Application `
    -ProviderName Contoso-App `
    -Id 1000 `
    -Message 'Service started' `
    -CreateSource `
    -Confirm:$false
```

Source registration is a separate administrative action. `-CreateSource`
makes that opt-in explicit; subsequent writes do not need elevation when the
registered source and log permissions allow them.

### Registered manifest providers

Write an existing provider by ID and ordered payload:

```powershell
Write-EVXEvent `
    -ProviderName Microsoft-Windows-PowerShell `
    -Id 4100 `
    -Payload @('Context', 'User data', 'Payload') `
    -Confirm:$false
```

For an EventViewerX-managed custom provider, write by stable event and field
names:

```powershell
Write-EVXEvent `
    -ProviderName Contoso.Scanner `
    -EventName ScanCompleted `
    -Data @{
        ComputerName = $env:COMPUTERNAME
        FindingCount = 7
    } `
    -Confirm:$false
```

The engine resolves the exact event version, orders named fields according to
the registered schema, converts declared native types, and enforces ETW size
limits. See [Custom providers](Custom-Providers.md) and
[`Write-Events.ps1`](../Examples/Write-Events.ps1).

## Command map

| Job | Commands |
| --- | --- |
| Query/filter/report | `Get-EVXEvent`, `New-EVXFilter`, `Show-EVXEvent` |
| Direct export | `Export-EVXEvent` |
| Durable progress | `Reset-EVXEventCheckpoint` plus checkpoint parameters on `Get-EVXEvent` |
| Real-time events | `Start-EVXWatcher`, `Get-EVXWatcher`, `Stop-EVXWatcher` |
| Provider/channel catalog | `Get-EVXProvider`, `Get-EVXLog`, `Test-EVXLog` |
| Channel/archive administration | `Set-EVXLog`, `Clear-EVXLog`, `Update-EVXLogArchive` |
| Classic log/source lifecycle | `New-EVXLog`, `Remove-EVXLog`, `New-EVXSource`, `Remove-EVXSource` |
| Collector subscriptions | `New-EVXCollectorSubscription`, `Get-EVXCollectorSubscription`, `Set-EVXCollectorSubscription` |
| PowerShell recovery | `Get-EVXPowerShellScript` |
| Event writes | `Write-EVXEvent` |
| Provider definitions/packages | `Test-EVXProviderDefinition`, `New-EVXProviderPackage`, `Get-EVXProvider`, `Install-EVXProviderPackage`, `Uninstall-EVXProviderPackage` |

Use `Get-Help <command> -Full` for the parameter-level reference generated from
the compiled cmdlet XML documentation.
