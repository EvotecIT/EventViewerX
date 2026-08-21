# Troubleshooting

## A query is slow

1. Use a native filter (`EventId`, provider, time, level, named data) instead of
   filtering after the pipeline.
2. Select the least expensive `ReadMode`. Message formatting is often the
   dominant cost.
3. Use `Metadata` for counting/discovery, `StructuredData` for payload
   analysis, and `RawXml` for byte-stable interchange.
4. Use `Export-EVXEvent` for large file output instead of `Export-Csv` or
   `ConvertTo-Json` in a PowerShell object pipeline.
5. Avoid `@(...)`, `ToList()`, or another operation that intentionally retains
   every event.
6. For several independent sources, tune `MaxConcurrency`; for remote readers,
   keep `BufferCapacity` bounded.

The [benchmark contract](../Benchmarks/EventLogParsing/README.md) distinguishes
exact-output, common-projection, and native-format comparisons. Compare tools
only when source, filters, ordering, fields, message culture, output bytes,
warm-up, and hash timing are recorded.

## Messages are missing or localized differently

Provider messages are produced from installed provider resources. The event
payload alone may not contain the human-readable template.

```powershell
Get-EVXEvent `
    -LogName System `
    -ReadMode Message `
    -MessageCulture en-US `
    -FallbackMessageCulture de-DE
```

Inspect message render status on the result. Common causes are:

- the requested culture is not installed in the provider resources;
- the provider resource DLL is absent on the reading machine;
- an offline EVTX was moved without archived resources;
- access to the resource or provider metadata failed.

Use `Update-EVXLogArchive` or `Export-EVXEvent -Format Evtx
-ArchiveResources` before moving an archive when later message rendering
matters.

## Remote query fails

Verify:

- the Windows Event Log service is running on the target;
- the Windows Event Log RPC firewall rules are enabled;
- TCP 135 and dynamic RPC connectivity are allowed;
- the account can read the selected channel;
- DNS resolves the intended computer;
- the chosen authentication mode is supported in that trust relationship.

Use a short connection timeout for fan-out discovery and
`-ContinueOnError` so one host does not discard healthy results:

```powershell
Get-EVXEvent `
    -LogName System `
    -MachineName DC01, DC02 `
    -SessionTimeoutMs 3000 `
    -ContinueOnError `
    -MaxEvents 100
```

The local machine name, `localhost`, loopback, short host name, and the local
FQDN use the local native path rather than opening an unnecessary RPC session.

## Access is denied

Reading Security and some analytic/debug channels may require an elevated
identity or membership in Event Log Readers. Creating/removing classic sources
and logs, changing channel policy, editing collector subscriptions, and
installing/uninstalling manifest providers normally require elevation.

Do not solve read access by granting broad channel SDDL unless that is an
intentional security decision. Prefer a narrowly scoped service identity or
Event Log Readers membership.

## A checkpoint replays or stops

Checkpoints are scoped to source identity and log generation. A cleared,
recreated, replaced, or wrapped log can invalidate a previous record boundary.
Inspect the checkpoint sidecar and reset intentionally:

```powershell
Reset-EVXEventCheckpoint `
    -Path C:\State\FailedLogons.json `
    -Confirm:$false
```

Do not share one checkpoint path between unrelated queries or concurrent jobs.
The store uses locking and atomic updates, but one state file still represents
one logical polling workflow.

## A watcher loses events or grows work

Watcher buffers are bounded. Keep the action small and non-blocking. If the
handler performs network calls, database writes, or expensive formatting, pass
the event to a separately bounded worker queue. Select `Metadata` or
`StructuredData` when messages are not required.

Choose the start contract deliberately:

- `Future` for events arriving after subscription;
- oldest/existing when a bounded historical catch-up is intended;
- bookmark start when another durable component owns the bookmark.

## An EVTX file cannot be read

- Confirm the path is a complete offline `.evtx`, not a live channel backing
  file copied while active.
- Use `Test-EVXLog` for a health probe.
- Try `RawXml` or `Metadata` to distinguish structural read failure from
  provider-message rendering failure.
- Preserve original ordering expectations with `-Oldest`.
- A native EVTX export is local-only; remote CSV/JSONL/XML are written on the
  caller through bounded streaming.

## Provider package build fails

Package building runs entirely inside EventViewerX and does not require the
Windows SDK, Visual Studio, MSVC, or an external compiler. Check the structured
validation errors first: invalid identities, unresolved metadata references,
incompatible field output types, oversized dimensions, and breaking baseline
changes are rejected before an artifact is emitted.

Also confirm that the output directory is writable and that an existing
package is replaced only with `-Force` (PowerShell) or `Overwrite = true`
(C#). A build failure should not be worked around by installing SDK tools.

## Provider package installation fails

Check:

- the shell is elevated;
- the package hashes/signature validate;
- the configured trust mode and signer thumbprint match the package;
- provider name and GUID match the installed provider;
- the upgrade was built against the released baseline;
- a downgrade or same-version replacement was explicitly allowed when intended;
- the custom installation root is empty or already managed by EventViewerX.

Reinstalling the exact active package is the supported repair operation. The
installer reconstructs verified files and registration from retained package
bytes.

## Historical custom-provider messages no longer render

Uninstall without `-RemoveFiles` to retain schema and message resources.
Removing those files can make historical events unreadable even though their
raw structured payload is still present. Keep released provider packages in
release storage and archive resources with important EVTX files.

## File removal is pending a reboot

Windows can retain a mapped message-resource DLL after provider
unregistration. `Uninstall-EVXProviderPackage -RemoveFiles` reports
`FileRemovalPendingReboot` instead of treating the locked DLL as a half-success.
The managed tree is moved/scheduled for deletion safely; reboot completes it.

## PowerShell script recovery reports truncation

`MaxEventsScanned`, `MaxPendingScripts`, and `MaxCachedEvents` are safety
bounds. Increase them deliberately when the source is trusted and the host has
capacity. Scan-limit truncation is reported only when a one-record lookahead
proves another matching event exists.

## Collect useful diagnostics

Record:

- PSEventViewer and EventViewerX versions;
- PowerShell edition/version or .NET target;
- local, remote, structured, or offline source;
- complete filter and read mode;
- requested/fallback message cultures;
- event count and ordering;
- the exception including inner exceptions;
- whether the same identity can open the source in Event Viewer;
- for packages, provider name/GUID/version, package SHA-256, trust mode, and
  signer thumbprint (never a private key).

Use a small reproducible event range when reporting a functional defect. For a
performance report, use the published benchmark harness so the result includes
provenance and output hashes.
