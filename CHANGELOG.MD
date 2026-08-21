### Changelog

#### 4.0.0 - Unreleased

- Replaced the monolithic `SearchEvents` implementation with reusable query,
  batch, named-event, subscription, catalog, administration, export, and
  event-writing engines.
- Added a dependency-free native Windows Event Log reader for local channels,
  remote channels, structured QueryList XML, and offline EVTX files.
- Added explicit `Metadata`, `Message`, `StructuredData`, `RawXml`, and `Full`
  read modes, deterministic provider-message cultures with event-specific
  fallback, render status, lazy payload work, 64-bit limits, native bookmarks,
  and durable per-source checkpoints.
- Added Get-WinEvent-compatible hashtable/XML/XPath filtering, provider-only
  queries, wildcard and multi-source expansion, QueryList diagnostics, bounded
  concurrency, cancellation, and per-source failure continuation.
- Added bounded direct CSV, JSON Lines, XML, and native EVTX exports with
  temporary-file promotion, optional SHA-256, and archive-resource support.
- Added native subscriptions, hardened watchers, provider and channel catalogs,
  health probes, channel policy, archive maintenance, classic log/source
  management, and Windows Event Collector subscription management.
- Added `Write-EVXEvent` for registered manifest/ETW providers and retained
  `Write-EVXEntry` as the explicit classic Event Log write contract.
- Rebuilt named-event querying on the shared engine and added bounded optional
  enrichment with ordered checkpoint semantics.
- Added reproducible PowerForge benchmark contracts for common public work,
  byte-identical output, and intentionally different native schemas.
- Removed stale examples, placeholder rules, duplicate implementations, and
  historical command aliases. `Find-WinEvent` is the only retained alias.
- **Breaking:** C# consumers must use the focused engine APIs instead of
  `SearchEvents`. General queries now default to `ReadMode Message`, bookmarks
  are opt-in, and the PowerShell surface exposes only canonical EVX commands.

#### 2.3.0 - 2024.11.25 *EXPERIMENTAL*
- Fix for SMBv1
- `BuildWinEventFilter` adds newlines between clauses for easier debugging when `xpathOnly` is `$false`

**Full Changelog**: https://github.com/EvotecIT/PSEventViewer/compare/v2.2.0...v2.3.0

#### 2.2.0 - 2024.11.25 *EXPERIMENTAL*
* Add smbv1 by @PrzemyslawKlys in https://github.com/EvotecIT/PSEventViewer/pull/21

**Full Changelog**: https://github.com/EvotecIT/PSEventViewer/compare/v2.1.0...v2.2.0

#### 2.1.0 - 2024.08.30 *EXPERIMENTAL*
- Fixes missing alias for `Write-WinEvent` for one of properties

#### 2.0.0 - 2024.07.21 *EXPERIMENTAL*
- Converted `Write-Event` to `Write-WinEvent` as binary cmdlet now
- Added `Find-WinEvent` binary cmdlet which is a replacement for `Get-Events`. It's complete rewrite of `Get-Events`, it's much faster and provides unique features known from `PSWinReporting` module as offered by `Find-Events` function

#### 1.0.22 - 2022.05.26
- Fixed `Set-EventsSettings` not setting values as required

#### 1.0.21 - 2022.05.25
- Added alias to parameter in `Set-EventsSettings`

#### 1.0.20 - 2022.05.25
- Improved `Get-EventsSettings` to work using native ways rather than playing with registry settings, also more options
- Improved `Set-EventsSettings` to work using native ways rather than playing with registry settings, also more options

#### 1.0.19 - 2022.05.18
- Performance improvements
- `ObjectAffected` property not reporting when `TargetDomainName` is empty - [#16](https://github.com/EvotecIT/PSEventViewer/issues/16)
#### 1.0.18 - 2022.04.02
- Improved documentation
- Signed module

#### 1.0.17 - 2020.05.31
- Fix for `Get-Events` (use of Path and NamedDataFilter) - provided by [danubie #13](https://github.com/EvotecIT/PSEventViewer/pull/13) - solves [#12](https://github.com/EvotecIT/PSEventViewer/issues/12)

#### 1.0.16 - 2020.05.31
- Fix for `Get-Events` for NamedDataFilter - provided by [danubie #11](https://github.com/EvotecIT/PSEventViewer/pull/11) - solves [#10](https://github.com/EvotecIT/PSEventViewer/issues/10)

#### 1.0.15 - 2020.05.17
- Fix for `Get-EventsFilter` - provided by [danubie #9](https://github.com/EvotecIT/PSEventViewer/pull/9) - solves [#7](https://github.com/EvotecIT/PSEventViewer/issues/7) and [#8](https://github.com/EvotecIT/PSEventViewer/issues/8)

#### 1.0.14 - 2020.04.11
- Updates to PSD1

#### 1.0.13 - 2020.02.16
- Added Get-EventsSettings/Set-EventsSettings (Work in progress)

#### 1.0.12 - 2020.01.01
- Added some new aliases

#### 1.0.11 - 2019.12.30
- Added Write-Event

#### 1.0.10 - 2019.12.17
- Path is back. Not sure why it was gone. Need improvements.

#### 1.0.9 - 2019.11.12
- Removed dependency on PSSharedGoods on the published module
- PSSharedGoods is still dependency, but the building process makes it possible to compile it and push to PSGallery/Releases without that dependency.

#### 1.0.7 - 2019.09.12
- Small changes to Get-EventsInformation

#### 0.62 - 2019.01.11
- Fix for Member Name with a comma inside

#### 0.61 - 2019.01.02
- Multiple new parameters, some new functionality

#### 0.51
- Added -RecordID parameter (currently it only works with LogName + RecordID, you can't use any other parameters with RecordID as it will take LogName + RecordID anyways and crash if it's not there)

#### 0.50
- A version that worked fine :-)
