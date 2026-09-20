# Operational detection packs

This document describes the current 4.0 source tree. Version 4.0 is not yet
published or released.

EventViewerX groups reusable detections into versioned packs instead of adding
a PowerShell command for every administrative scenario. A pack owns rule IDs,
versions, hashes, provenance, source coverage, executable fixtures, and tuning.
The PowerShell module and CLI remain thin surfaces over the same core plan.

The source currently includes five `1.0.0` packs:

| Pack ID | Operational focus |
| --- | --- |
| `eventviewerx.eventing-integrity` | Log clear/full, audit-policy change, crash-on-audit-fail recovery, and time changes. |
| `eventviewerx.identity-privilege` | Privileged group membership, SID history, account lifecycle, user rights, privilege use, deletion, and lockout bursts. |
| `eventviewerx.authentication-modernization` | NTLMv1, failed-logon correlation, weak Kerberos, LDAP signing, SMB1, Kerberos policy, and RC4 enforcement risk. |
| `eventviewerx.governance` | Group Policy, Certificate Services, BitLocker suspension, and recovery-material changes. |
| `eventviewerx.endpoint-protection` | Defender, scheduled tasks, firewall rules, network drivers/promiscuous mode, and removable devices. |

## Admin workflow

```powershell
# Inventory immutable content and source requirements.
$packs = Get-EVXDetectionPack
$packs | Select-Object PackId, Version, Hash
Get-EVXDetectionCoverage

# Validate positive, negative, boundary, and known-benign fixtures.
Test-EVXDetectionPack

# Prove that the target fleet can supply the required channels and policies.
Test-EVXReadiness -Scenario AuthenticationMonitoring -ActiveDirectory CurrentForest

# Run all built-in packs, or pass selected versioned packs explicitly.
Get-EVXEvent -Type ActiveDirectoryAuthentication -TimePeriod Last24Hours |
    Invoke-EVXDetection

$selected = Get-EVXDetectionPack -PackId `
    'eventviewerx.authentication-modernization', 'eventviewerx.eventing-integrity'
Get-EVXEvent -Type ActiveDirectoryAuthentication -TimePeriod Last24Hours |
    Invoke-EVXDetection -Pack $selected
```

Tuning suppresses or adjusts known behavior without rewriting pack identity.
Coverage remains part of the result, so an empty finding set is not reported as
complete when required telemetry is absent. External Sigma rules can be wrapped
in an integrity-protected pack with an explicit ID/version after compilation;
they still execute in the same bounded engine.

Add a new pack only when a coherent operational owner, source contract, and
fixture set exist. Add a new rule to an existing pack when the owner and
coverage contract are unchanged. Do not create a new cmdlet merely to expose a
new rule or scenario.
