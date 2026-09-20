# Sigma compatibility

This document describes the current 4.0 source tree. Version 4.0 is not yet
published or released.

EventViewerX compiles supported Sigma YAML into the same immutable native
detection plan used by built-in rules. It does not embed a second detection
engine. Unsupported syntax or unsafe source assumptions remain explicit
diagnostics and block import.

## Two compilation modes

`Strict` is the default. A rule must provide selectors EventViewerX can compile
without guessing, such as an explicit Windows channel, provider, service, or
event ID. This mode is the right default for mixed fleets and evidence that
must be portable between telemetry configurations.

`WindowsSysmonAndPowerShell` is an explicit, versioned telemetry profile. It
maps well-known Sigma categories to exact Microsoft-Windows-Sysmon/Operational
or Microsoft-Windows-PowerShell/Operational selectors. Selecting it asserts
that those channels are enabled, retained, and collected. An explicit EventID
in a rule remains authoritative. A conflicting service or product is rejected.

The built-in profile identity is:

- profile ID: `windows-sysmon-powershell`
- contract version: `1.0.0`
- Sysmon categories: process, network, driver/image load, remote thread, raw
  access, process access, file, registry, pipe, WMI, DNS, tampering, and Sysmon
  status/error categories with exact documented event IDs
- PowerShell categories: module logging event 4103 and script block logging
  event 4104

High-risk ambiguous categories such as generic file access/change/rename and
legacy PowerShell classic-log categories are not guessed by this profile.

```powershell
# Lossless strict validation remains the default.
Test-EVXSigmaRule -Path .\Rules\*.yml

# Opt in only when this telemetry contract matches the target fleet.
$result = Test-EVXSigmaRule -Path .\Rules\*.yml `
    -TelemetryProfile WindowsSysmonAndPowerShell

$rules = Import-EVXSigmaRule -Path .\Rules\*.yml `
    -TelemetryProfile WindowsSysmonAndPowerShell
```

The CLI exposes the same choice:

```powershell
evx detect --path .\Security.evtx --sigma .\Rules\*.yml `
    --sigma-profile windows-sysmon-powershell --explain
```

## Pinned SigmaHQ audit

The reproducible audit uses the SigmaHQ `sigma` repository at exact commit
`2e8fd89f82d9104c1b30321a307254ddeea17de2`. The audit executable verifies the
40-character commit and a clean tracked corpus before reading rules. It emits
JSON and Markdown reports and rejects unknown command-line options.

For the 2,410 Windows rules at that commit:

| Mode | Supported | Unsupported | Compatibility |
| --- | ---: | ---: | ---: |
| Strict | 253 | 2,157 | 10.50% |
| Windows Sysmon and PowerShell 1.0.0 | 2,213 | 197 | 91.83% |

The remaining 197 rules are not silently approximated. The leading diagnostic
classes are 90 unsupported field/operator constructs, 74 source contracts that
still lack a safe exact mapping, 19 intentionally unmapped categories, 6
unsupported conditions, 5 unsupported correlation constructs, and 3 other
unsupported selection constructs.

Run the audit from a pinned, clean sparse checkout whose corpus root contains
the `windows` directory:

```powershell
dotnet run --project .\Sources\EventViewerX.SigmaAudit\EventViewerX.SigmaAudit.csproj `
    -c Release -- `
    --corpus C:\Temp\sigma\rules `
    --commit 2e8fd89f82d9104c1b30321a307254ddeea17de2 `
    --scope windows `
    --profile windows-sysmon-powershell `
    --output-json .\Artifacts\sigma-windows.json `
    --output-markdown .\Artifacts\sigma-windows.md
```

Compatibility is a corpus-at-commit measurement, not a promise that every
future SigmaHQ rule will compile. Pin the corpus, keep the generated report,
and review diagnostics whenever the corpus or profile version changes.
