# Captured endpoint investigations

EventViewerX reads captured CMTrace and plain-text logs, reconstructs attributable
Intune application attempts, and interprets captured `dsregcmd /status` output.
The workflow runs offline. It copies and hashes original files before parsing,
retains diagnostic records and collection context in an investigation session,
and produces findings with evidence coordinates and next checks.

The endpoint extension uses the existing investigation manifest and integrity
checks. Windows event observations and their detection plan can be supplied to
`EventInvestigationSession.CreateEndpoint` alongside endpoint artifacts. Text
records keep their source format and never acquire synthetic event IDs or UTC
timestamps.

## Create and replay through the CLI

```powershell
evx investigation create --directory .\Case-001 --log .\AppWorkload.log,.\AppActionProcessor.log --dsreg .\dsreg-status.txt --device Endpoint-001 --context User --captured-at 2026-10-09T10:00:00Z --expected-join Hybrid --html .\Case-001.html
evx investigation inspect --directory .\Case-001
evx investigation replay --directory .\Case-001 --html .\Case-001-replay.html
```

Use a new session directory and new report files. Presentation exports belong
outside the session. `inspect` returns the retained analysis; `replay` evaluates
the verified canonical diagnostic records using the same collection context.
A different engine build requires `--allow-different-engine` and marks the
comparative result incomplete. Neither command executes a live collection query.

`--facts` accepts a JSON object. `--registry` retains a registry snapshot without
importing it. `--attachment` retains other original evidence without executing it.
These artifacts are hashed and listed in the manifest; unsupported policy facts
are preserved for inspection rather than used to invent conclusions.

Declare `--device` only when the inputs belong to that endpoint. Cross-file
application correlation also requires known instants and a specific execution
identity in the records. A source generation containing any unknown instant stays
entirely in byte order. Unknown context and generic `User` context, regardless of
casing, keep attempts source-local. CLI capture options apply to all supplied
files; use the typed API or PowerShell capture descriptors when files have
different contexts, offsets, or capture times.

## Create and replay through PowerShell

```powershell
Import-Module PSEventViewer

$capture = [EventViewerX.EventEndpointCapture]@{
    ExpectedJoin = 'Hybrid'
    Inputs = @(
        [EventViewerX.EventEndpointInput]@{
            Path = '.\AppWorkload.log'
            Kind = 'Log'
            Device = 'Endpoint-001'
            UtcOffset = [TimeSpan]::FromHours(2)
        }
        [EventViewerX.EventEndpointInput]@{
            Path = '.\dsreg-status.txt'
            Kind = 'DsRegCmd'
            ExecutionContext = 'User'
            CapturedAt = [DateTimeOffset]::Parse('2026-10-09T10:00:00Z')
        }
    )
}

$session = Export-EVXInvestigation -Path .\Case-001 -EndpointCapture $capture
$opened = Open-EVXInvestigation -Path .\Case-001
$analysis = $opened | Invoke-EVXInvestigation -Endpoint -HtmlPath .\Case-001.html
$analysis.Applications | Select-Object ApplicationId, Context, AttemptId, LastPhase, Outcome
$analysis.Findings | Select-Object Title, Status, Explanation, NextChecks
```

Add native event observations through the pipeline and supply `-Manifest` and
`-Plan` to retain their declared event window and detection rules. Without native
events, the endpoint capture creates an empty native plan. The default
`Invoke-EVXInvestigation` continues to replay native Windows-event detection;
`-Endpoint` selects diagnostic analysis.

## Read a bounded log batch

```powershell
$options = [EventViewerX.EventDiagnosticReadOptions]@{
    FinalFile = $false
    MaximumRecords = 1000
    UtcOffset = [TimeSpan]::FromHours(2)
}
$batch = Get-EVXDiagnostic -Path .\IntuneManagementExtension.log -Options $options
$batch.Records | Select-Object Source, LineStart, LineEnd, Timestamp, TimeQuality, Message
# After durably accepting the batch, retain its checkpoint for the next read.
$next = Get-EVXDiagnostic -Path .\IntuneManagementExtension.log -Options $options -Checkpoint $batch.Checkpoint
```

The reader supports UTF-8 and BOM-marked UTF-16. It bounds bytes per batch,
records per batch, and retained bytes per frame. Incomplete trailing frames stay
pending. Oversized completed frames carry an explicit diagnostic and cannot
produce application conclusions. A frame larger than the batch bound cannot
advance until the bound is increased; session capture reports that gap instead
of repeatedly retrying it. Concurrent truncation discards an unfinished frame and
reports the changed input so the next read can validate its generation.

The checkpoint fingerprints physical file identity, the prefix, and the preceding
4 KiB. Rotation, empty truncation, and changes in those checked bytes start a new
generation. In-place historical changes elsewhere are outside the bounded check;
use an immutable copied investigation for full-file integrity. The collector must
durably accept records before committing a checkpoint. The CLI `diagnostics read`
can retain the position with `--checkpoint`, but cannot make stdout and an external
consumer transaction atomic.

CMTrace offsets are signed minutes. An embedded offset takes precedence; use
`--utc-offset-minutes` or `UtcOffset` when the writer's offset is known. Missing
offsets remain unknown. The reader never applies the collector's local timezone.

## Interpret a number in context

```powershell
Get-EVXDiagnostic -Code '-2147024891' -Kind HResult
Get-EVXDiagnostic -Code '3010' -Kind WindowsInstaller
evx diagnostics code --value 0x80070005 --kind HResult
```

The result retains the original number, its unsigned and hexadecimal forms,
HRESULT facility and severity where applicable, and a next check. Common Win32,
HRESULT and Windows
Installer values include documented symbolic names, deterministic explanations
and a specific next artifact or configuration to inspect. Unknown numbers retain
their representation and family semantics without an invented diagnosis. The
catalog does not depend on the collector's language or operating system.
Application findings retain that interpretation when the captured line declares
Windows Installer; other executable exit codes still require their configured mapping.

A process exit code needs the application's configured return-code mapping. Windows Installer
0, 1641, and 3010 have success semantics; a required restart still needs reboot
policy and later detection evidence. [Windows Installer return-code contract](https://learn.microsoft.com/en-us/windows/win32/msi/error-codes).

## Read conclusions and preserve evidence

Application analysis recognizes applicability, requirements, dependencies,
detection, download, enforcement, restart, and reporting messages. Records need
one explicit application ID. Unattributed lines remain in evidence and never
inherit an app from a nearby line or thread. Attempt boundaries without explicit
IDs are marked inferred. An observed client detection result does not prove
service-side reporting or later deployment state. A negative detection during an
unfinished installation remains uncertain; post-enforcement failure needs a
completion or exit result, or an explicit terminal failure. [Microsoft's documented application lifecycle](https://learn.microsoft.com/en-us/troubleshoot/mem/intune/app-management/develop-deliver-working-win32-app-via-intune).

DSRegCmd analysis retains section-local values, unknowns, and conflicting fields.
Join-state mismatch needs an explicit intended join mode. User PRT and Hello
results need the affected user's context and policy. MDM URLs alone do not prove
device enrollment. Capture time must be supplied by the collector; a file's
modification time is not a collection receipt. [Microsoft's DSRegCmd field contracts](https://learn.microsoft.com/en-us/entra/identity/devices/troubleshoot-device-dsregcmd).

Captured DSRegCmd text and JSON facts support UTF-8 and BOM-marked UTF-16, including
Windows PowerShell 5.1 redirection. Canonical session JSON remains strict UTF-8.
Replay checks the hash of the diagnostic bytes actually consumed as well as the
session's preflight integrity checks.

Original artifacts and canonical diagnostic records remain sensitive. Reports
show findings and captured evidence together. Select a finding, open its Evidence
tab, then select a supporting log coordinate or identity artifact. The report
selects the corresponding record, including records outside the current table
page or filter. Artifact summaries retain capture context and time; raw fields
remain in the original session unless explicitly included in the export.

Original artifacts and canonical diagnostic records remain sensitive. Reports
omit raw log messages and DSRegCmd fields by default; application identifiers and
conclusions can still be sensitive. Use `--include-sensitive` or
`-IncludeSensitiveEvidence` only when the report recipient needs raw evidence.
This report option is data minimization, not anonymization. Keep an original
session separate from a presentation export.
