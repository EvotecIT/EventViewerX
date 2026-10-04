# Privacy policies and evidence exports

EventViewerX creates reusable report snapshots before rendering or transporting
them. Use a privacy policy to create a detached export from that snapshot. The
original rows and durable history retain their evidence.

The default policy omits messages, all payload values, raw normalization evidence,
computer and container identities, activity identifiers, report descriptions, and
free-text source diagnostics. Event timestamps, event and record IDs,
provider, source channel, level, process and thread IDs remain visible. This is
data minimization, not an anonymity guarantee: those retained fields can still
identify an event to someone who has other evidence.

Exported rows use one standard generic schema. Original type labels and section
names are omitted; event IDs and providers remain available for classification.
The export can be rehydrated or rendered through the normal report pipeline.

## Select the exported data

PowerShell uses the same core policy as C# and the CLI:

```powershell
$policy = [EventViewerX.Reporting.EventReportPrivacyOptions]::new()
$report = Show-EVXEvent -Type ADUserLogonFailed -Collector WEC01 `
    -DateFrom (Get-Date).AddHours(-1) -MaxEvents 10000 -PassThru
$export = $report | Show-EVXEvent -Privacy $policy -PassThru
$export | Show-EVXEvent -HtmlPath .\FailedLogons.html -CsvPath .\FailedLogons.csv
```

When `-Privacy` and `-StorePath` are supplied together, the store receives the
original snapshot; renderers and `-PassThru` receive the detached export. Apply
the policy before handing the snapshot to another application.

Inspect `Rows[0].Values.Keys` or the section schema to select payload field names.
Policies use actual payload names, not display labels or predicate aliases.
`RetainedValueFields` explicitly keeps selected scalar values.
`PseudonymizedValueFields` replaces selected scalars with HMAC-SHA256 tokens.
Common metadata and normalization fields cannot be selected as payload fields.
Nested objects and collections require an explicit scalar projection rather than
an implicit serialization of all their contents.

```powershell
$policy.PseudonymizedValueFields = [string[]] @('Who')
$policy.PseudonymizeSourceIdentities = $true
$key = [IO.File]::ReadAllBytes('.\report-key.bin')
$export = $report | Show-EVXEvent -Privacy $policy -PseudonymizationKey $key -PassThru
```

Use a randomly generated key of at least 32 bytes for the core or PowerShell API.
The CLI accepts a file containing exactly 32 binary bytes. Keep it separately
from exported reports and bundles. The same key, field name, value type and exact
value produce the same token; tokens are scoped to the field name. Different
keys produce different tokens. Pseudonymization does not remove retained event
metadata or make free-text fields safe to disclose.

```text
evx report --type ADUserLogonFailed --collector WEC01 --since 01:00:00 --max 10000 --privacy omit --html FailedLogons.html --bundle FailedLogons.zip --require-complete
evx report --type ADUserLogonFailed --collector WEC01 --since 01:00:00 --max 10000 --privacy pseudonymize --privacy-key-file report-key.bin --pseudonymize-fields Who --bundle FailedLogons-Keyed.zip
```

Add `--retain-fields FIELD[,FIELD]` only for payload values that may remain
visible. Privacy applies to report artifacts and their completion sidecars;
collection diagnostics on the console remain operational diagnostics.

## Bundle and verify a report

An evidence bundle contains `rows.jsonl`, detached `schemas.json`, and a versioned
`manifest.json`. The manifest records the producing assembly version, generation
time, source coverage, candidate and result counts, incomplete-input evidence,
and the SHA-256 checksum and uncompressed length of every content member.

The CLI includes the resolved query and custom definition when applicable to an
ordinary file, channel or stored report. A privacy export omits that provenance:
filters, definition text and paths can contain the identities being removed.
Its `privacy.json` member identifies the omitted or keyed payload policy without
including the key. The core writer records the supplied `PrivacyPolicy` choices;
apply that policy to the report before writing the bundle.
The core API accepts separately selected query, definition and detection pack
provenance JSON when disclosure is appropriate.

Rendered HTML, Excel, CSV and email outputs selected in the same CLI invocation
are also included. A multi-schema CSV archive is named `report-csv.zip`; a single
CSV includes its completion metadata companion. The bundle does not embed raw
EVTX files, stored databases or pseudonymization keys.

```text
evx report --path saved.evtx --oldest --max 0 --html Report.html --excel Report.xlsx --bundle Evidence.zip --summary-file Completion.json --require-complete
evx bundle verify --path Evidence.zip
```

`--require-complete` returns exit code 2 when limits, diagnostics or source
failures make the selected input incomplete. The report is still available with
its completion evidence. Successful execution without that switch returns 0;
invalid arguments, failed export or failed verification return 1. Cancellation
returns 130. A successful query is not proof that the source retained every
historical event.

The verifier checks the exact supported inventory, checksums, lengths, row count,
completion flags and byte bounds without extracting files. It rejects missing or
extra members, duplicate names, directory traversal, unsupported schemas and
contradictory completion evidence. Checksums establish internal byte integrity,
not the authenticity of the producer. Protect or sign the resulting bundle
separately when authenticity is required.

The default uncompressed limit is 256 MiB. The core option
`MaximumUncompressedBytes`, CLI `--bundle-max-bytes`, and verifier `--max-bytes`
can select a bound between 1 MiB and 2 GiB. Larger datasets should be divided
into bounded snapshots. CLI bundle publication uses a temporary file and a new
destination; choose another filename when a bundle already exists.

## Compose the core APIs

```csharp
var policy = new EventReportPrivacyOptions {
    PseudonymizedValueFields = new[] { "Who" },
    PseudonymizeSourceIdentities = true
};
EventReport exported = EventReportPrivacy.Apply(report, policy, key);

using var output = File.Create("Evidence.zip");
EventEvidenceBundle.Write(exported, output, new EventEvidenceBundleOptions {
    PrivacyPolicy = policy
});
output.Flush();
output.Position = 0;
EventEvidenceBundleManifest verified = EventEvidenceBundle.Verify(output);
```

The stream API leaves streams open and gives the caller responsibility for
atomic publication. A failed or canceled write is a partial stream and must not
be published. Supply optional artifacts as readable caller-owned streams through
`EventEvidenceBundleOptions.Reports`; query, definition and pack JSON are
explicit provenance choices, independent of the report privacy policy.
