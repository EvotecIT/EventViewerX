---
Module Name: PSEventViewer
Module Guid: 5df72a79-cdf6-4add-b38d-bcacf26fb7bc
Download Help Link: https://github.com/EvotecIT/PSEventViewer
Help Version: 4.0.0
Locale: en-US
---
# PSEventViewer Module
## Description
High-performance typed Windows Event Log queries, reports, exports, watchers, WEC, custom providers, diagnostics, and administration for PowerShell.

## PSEventViewer Cmdlets
### [Clear-EVXLog](Clear-EVXLog.md)
Clears Windows Event Log channels through the native engine.

Supports local or remote channels, explicit credentials, and an optional native EVTX backup. Failures are terminating and retain their Windows error code.

### [Export-EVXEvent](Export-EVXEvent.md)
Streams Windows events directly to CSV, JSON Lines, XML, or native EVTX.

Uses the EventViewerX native engine and writes directly to the destination without materializing PowerShell objects. Completed output is promoted atomically, so cancellation or failure does not replace an existing file.

### [Get-EVXCollectorSubscription](Get-EVXCollectorSubscription.md)
Returns normalized Windows Event Collector subscription configuration.

Reads local or remote WEC subscription inventory and returns detached snapshots with normalized XML details and query definitions. Remote access uses the caller's Windows identity.

### [Get-EVXEvent](Get-EVXEvent.md)
Enhanced event querying cmdlet that replaces and extends Get-WinEvent functionality.

Supports local and remote logs, built-in event types, custom JSON definitions, record ID resumes, parallel queries, and rich filtering.

### [Get-EVXLog](Get-EVXLog.md)
Retrieves event log details by name.

Lists log metadata (size, record count, status) on local or remote machines; supports wildcards.

### [Get-EVXPowerShellScript](Get-EVXPowerShellScript.md)
Retrieves reconstructed PowerShell scripts or execution-context records from event logs.

### [Get-EVXProvider](Get-EVXProvider.md)
Returns registered provider metadata or EventViewerX provider packages.

The default set supports local and remote provider discovery. Package sets inspect a portable .evxprovider file or list machine-wide EventViewerX-managed installations.

### [Get-EVXWatcher](Get-EVXWatcher.md)
Retrieves information about active EVX watchers.

Filters by watcher Id or Name and returns watcher metadata such as log, machine, filters, and runtime state.

### [Install-EVXProviderPackage](Install-EVXProviderPackage.md)
Installs or upgrades a portable custom Windows event provider package.

Verifies package hashes and signatures before changing machine state, enforces schema and version compatibility, stages resources under ProgramData, registers the manifest, verifies Windows metadata and channels, and rolls back to the previous provider if activation fails.

The target machine does not require the Windows SDK, Visual Studio, a C# compiler, generated source, or package build tools.

### [New-EVXCollectorSubscription](New-EVXCollectorSubscription.md)
Creates a typed collector- or source-initiated WEC subscription definition.

Builds safe Windows Event Collector XML from typed reports, custom definitions, a QueryList, or common event filters. The command does not change the collector; pipe the definition to Set-EVXCollectorSubscription to apply it.

### [New-EVXFilter](New-EVXFilter.md)
Creates a reusable typed Windows Event Log filter or compiles it to native query text.

The default output is EventViewerX.EventFilter for native event metadata. Supply Type or Definition to discover typed domain fields and build a reusable EventPredicate. Use AsXPath, LogName, or Path when native query text is required by Get-WinEvent, Event Viewer, or WEC.

### [New-EVXLog](New-EVXLog.md)
Creates a new Windows event log with optional size and retention settings.

Applies explicit desired state through ClassicEventLogManager and reports exactly what changed.

### [New-EVXProviderPackage](New-EVXProviderPackage.md)
Compiles a portable custom Windows event provider package.

Validates the schema, optionally compares a compatibility baseline, compiles the Windows event metadata and localized messages in-process, hashes every file, optionally signs package identity and hashes, and emits one portable .evxprovider file.

No Windows SDK, Visual Studio, native compiler, generated source, or external build tool is required.

### [New-EVXSource](New-EVXSource.md)
Registers a classic Windows Event Log source explicitly.

Creates only the requested source registration and supports provider message, parameter, and category resource files. The command reports whether it created anything.

### [Remove-EVXLog](Remove-EVXLog.md)
Removes an event log from the system.

Supports local or remote removal with ShouldProcess confirmation; useful for cleanup of custom logs.

### [Remove-EVXSource](Remove-EVXSource.md)
Removes an event source from Windows Event Log.

Deletes the provider registration locally or on a remote machine with optional log scoping.

### [Reset-EVXEventCheckpoint](Reset-EVXEventCheckpoint.md)
Resets persisted event-query checkpoint progress safely.

Starts a new checkpoint generation under the shared file lock so an in-flight query from the previous generation cannot restore stale progress. Use this cmdlet instead of deleting only the RecordIdFile compatibility file because generation state is stored in a visible companion .state.json file.

### [Set-EVXCollectorSubscription](Set-EVXCollectorSubscription.md)
Applies a typed local WEC subscription definition or changes its enabled state.

Definition input creates or updates a subscription through the Windows inbox collector utility. The state set uses the supported WEC API. Both paths verify persisted state. Definition apply is cancellable and time-bounded; failed apply is rolled back and reports explicitly when rollback cannot establish a known persisted state.

### [Set-EVXLog](Set-EVXLog.md)
Updates Windows Event Log channel policy.

Configures enabled state, maximum size, retention mode, file path, or security descriptor and returns a detailed per-log result.

### [Show-EVXEvent](Show-EVXEvent.md)
Queries or accepts EventViewerX events and creates polished HTML, Excel, or email output.

Show-EVXEvent uses one normalized report snapshot for every selected output. A Type owns its source channels and event IDs; LogName is reserved for generic event queries.

Typed and custom definitions render only their domain fields. Composite types keep each leaf schema in a separate table and Excel worksheet, while Event Provenance retains the technical Windows event context.

### [Start-EVXWatcher](Start-EVXWatcher.md)
Starts real-time monitoring of Windows Event Logs with customizable filters and actions.

Supports explicit event IDs or EventType, provider-side filtering, optional staging events, auto-stop conditions, and a callback for each match.

### [Stop-EVXWatcher](Stop-EVXWatcher.md)
Stops running EVX watchers by identifier, name, or en masse.

Requires exactly one selector and reports missing identifiers or names instead of silently doing nothing. Use PassThru to return each watcher that was stopped.

### [Test-EVXLog](Test-EVXLog.md)
Runs a bounded Windows Event Log connectivity and query probe.

Executes a metadata-only query through the same owned native reader used by Get-EVXEvent, within a fixed budget, and returns the newest matching timestamp plus optional channel metadata.

### [Test-EVXProviderDefinition](Test-EVXProviderDefinition.md)
Validates a custom Windows event provider definition.

Checks provider identity, channels, event versions, field references, maps, localization, Windows limits, and schema compatibility before any native build tools or machine registration are used.

### [Uninstall-EVXProviderPackage](Uninstall-EVXProviderPackage.md)
Unregisters an EventViewerX-managed custom event provider.

Removes the active manifest registration. Package and schema files are retained by default so archived EVTX records remain renderable and the provider can be restored; use RemoveFiles only when that history is no longer required.

### [Update-EVXLogArchive](Update-EVXLogArchive.md)
Archives provider resources into exported EVTX files.

Makes a Windows-native EVTX export self-contained for message rendering on computers that do not have the source provider installed.

### [Write-EVXEvent](Write-EVXEvent.md)
Writes classic Event Log entries or registered manifest/ETW events.

The Classic parameter set writes through a registered classic source. Manifest parameter sets resolve the registered event schema, validate native values, and write positional, named, or typed payloads.
