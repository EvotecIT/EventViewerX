# EventViewerX roadmap

EventViewerX is the active home of PSEventViewer, PSWinReporting, and PSWinReportingV2 development. This roadmap records product contracts that belong in EventViewerX 4.0 and ideas deliberately deferred. It is not a changelog; GitHub releases and pull requests record completed changes.

## Product boundaries

- A query without an explicit target reads the local computer.
- Domain, forest, collector, and trusted-forest expansion are opt-in.
- EventViewerX assesses prerequisites with the permissions users already have. Readiness does not silently change audit policy, channels, subscriptions, firewall rules, or scheduled tasks.
- Built-in behavior is compiled and typed. Portable definitions contain data, not arbitrary PowerShell or C# scripts.
- Raw events remain evidence. Normalization, grouping, enrichment, and aggregation add views without destroying observations.
- Partial failures, bounds, cancellation, and incomplete coverage remain visible.
- Event-driven enrichment may resolve identities already present in selected events. It must not turn a report into an unrequested AD or SYSVOL inventory.
- PowerShell 5.1, PowerShell 7, `evx.exe`, packed artifacts, and the .NET API describe the same contracts.

## Current 4.0 release gate

The 4.0 package is intentionally not published until this checklist is complete and proven from packed artifacts.

- [x] Consolidate repository history and retain frozen `PSWinReporting` and `PSWinReportingV2` branches.
- [x] Make local-machine discovery the default and Active Directory discovery explicit.
- [x] Add typed target discovery, requirements, readiness evidence, direct/WEC onboarding, and durable collection checkpoints.
- [x] Add persistent Group Policy context keyed only by selected audit events.
- [x] Deliver deterministic value normalization in normal report/export surfaces.
- [x] Deliver non-destructive transport and semantic occurrence grouping.
- [x] Deliver shared bounded aggregations, trends, and charts, including `Measure-EVXEvent` and safe SQLite pushdown.
- [x] Deliver the first complete security-monitoring presets and prerequisite evidence.
- [x] Expose persistent Group Policy context through normal PowerShell, CLI, and reporting paths.
- [x] Finish 4.0 migration, examples, generated help, dual-runtime tests, packed-artifact validation, and GitHub-generated release-note preparation.

## Delivered foundations

### Target discovery and readiness

`Get-EVXTarget`, `Get-EVXRequirement`, and `Test-EVXReadiness` own the public PowerShell experience. Their compiled engine supports local-machine default, explicit current-domain/current-forest discovery, named targets, opt-in trusted forests, bounded partial results, and typed channel, audit-policy, service, role, WEC, permission, and runtime evidence.

The canonical operator walkthrough is [Onboarding and prerequisites](Docs/Onboarding.md).

### Durable local collection

The SQLite event store owns normalized event rows, schemas, consumer checkpoints, native bookmarks, and atomic event-plus-checkpoint commits. Retention gaps, channel replacement, scan limits, and incomplete coverage fail closed rather than advancing progress past evidence that was not delivered.

### Persistent Group Policy context

`GroupPolicyAuditEngine` and `SqliteEventContextStore` retain event-derived GPO identity and name history. They distinguish the name at event time, a last-known historical name, and a currently live name. The store does not discover every GPO or scan SYSVOL.

## Active 4.0 capabilities

### 1. Deterministic value normalization

The normalizer registry converts known raw provider values into stable typed values and display text while retaining the raw value, normalizer identity/version, outcome, warnings, and lossless status.

Initial built-ins cover directory-operation resource IDs, userAccountControl masks, Active Directory FILETIME values and sentinels, SIDs, GUIDs, distinguished names, OIDs, multi-value fields, and Windows audit resource identifiers. Unknown values remain visible and do not become invented labels.

Acceptance:

- HTML, Excel, CSV, stored rows, PowerShell objects, and CLI JSON use one normalization contract;
- presentation surfaces show raw provenance when canonical output differs;
- malformed known values remain queryable with a typed diagnostic;
- normalization is invariant across culture and input order.

### 2. Non-destructive occurrence grouping

Occurrence grouping is opt-in and returns groups containing every source observation plus one deterministic representative.

- `None` leaves observations independent.
- `Transport` groups only exact source-event identity duplicated through direct/WEC transport.
- `Semantic` uses compiled causal identifiers such as operation, application, activity, transaction, or batch IDs within an explicit window.

Time proximity, actor, target, or message similarity alone never proves one occurrence. Bounds fail closed with an explicit metadata result; they do not silently merge, drop events, or look like a valid empty query. Occurrence summaries retain the source coverage envelope, while later aggregation uses the original deterministic representative of each group rather than summary fields. Incident reconstruction remains a separate deferred decision.

### 3. Shared aggregations, trends, and charts

`EventAggregationEngine` owns Count, DistinctCount, FirstSeen, LastSeen, and Rate. Definitions declare dimensions, UTC or timezone-aware buckets, null behavior, rate interval, top-N scope, ranking measure, and state bounds.

```powershell
Get-EVXEvent -Preset AuthenticationHealth -TimePeriod Last7Days |
    Measure-EVXEvent -GroupBy Type,SourceComputer -Bucket Day `
        -Top 10 |
    Show-EVXEvent -HtmlPath .\Authentication-Trend.html `
        -ExcelPath .\Authentication-Trend.xlsx
```

```text
evx measure --store C:\ProgramData\EventViewerX\events.db \
  --preset AuthenticationHealth --bucket Day --group-by Type,SourceComputer \
  --measure Count::Events --top 10 --html Authentication-Trend.html
```

SQLite pushdown is used only when stored selectors, text semantics, timezone, ranking, bounds, and measure fields have exact parity with the managed engine. Normalized/custom fields, Unicode-sensitive comparison, predicates, non-UTC/DST buckets, incompatible ranking, and bounded distinct state fall back automatically. Both modes return execution ownership and completeness evidence. A complete `EventReport` preserves its coverage through `Measure-EVXEvent`; loose pipeline rows are deliberately marked `Unknown`. CSV exports include the same completeness envelope, including a metadata row when a bounded or failed aggregation has no data rows.

### 4. First security-monitoring slice

Built-in leaf types, composites, prerequisites, and presets cover:

- scheduled-task lifecycle events 4698, 4699, 4700, 4701, and 4702;
- Windows Firewall rule events 4946, 4947, and 4948;
- Microsoft Defender events 1116, 1117, and 5007;
- NTLMv1 logons;
- Kerberos RC4/DES ticket use based on typed weak-encryption evidence;
- LDAP signing and cleartext-binding summaries/details.

Presets select compiled event types and exact predicates; they are not custom scripts. Readiness reports the audit subcategories, channels, roles, services, and volume warnings required by the selected slice.

### 5. Group Policy context in normal workflows

Persistent context is available without directly programming `GroupPolicyAuditEngine`:

```powershell
Get-EVXEvent -Type GroupPolicyDirectoryAudit `
    -ContextStorePath C:\ProgramData\EventViewerX\context.db `
    -TimePeriod Last7Days

Show-EVXEvent -Type GroupPolicyDirectoryAudit `
    -ContextStorePath C:\ProgramData\EventViewerX\context.db `
    -TimePeriod Last30Days `
    -HtmlPath .\GroupPolicy-Audit.html -ExcelPath .\GroupPolicy-Audit.xlsx
```

```text
evx report --type GroupPolicyDirectoryAudit \
  --context-store C:\ProgramData\EventViewerX\context.db \
  --since 30.00:00:00 --html GroupPolicy-Audit.html
```

This route reads oldest-first, buffers the selected timeline before resolution, and exposes event-time, last-known, and current GPO names with context state and reason. It never scans AD or SYSVOL. Non-shareable imported context requires an explicit caller-authorized partition.

### 6. Release and migration experience

The 4.0 migration guide explains which legacy workflows move to which EventViewerX command, collection choices, readiness before scheduling, built-in presets, persistent GPO context, normalization/grouping/completeness semantics, package names, supported runtimes, and frozen legacy branches.

## Deferred decisions

These remain outside the 4.0 release gate:

- incident reconstruction and general multi-event correlation;
- notification policy and transports while TeamsX transport work is in progress; Mailozaurr remains available to callers;
- custom branding and any paid-branding/licensing model;
- SQL Server or provider-neutral central storage and whether it belongs in a paid product;
- a managed Windows service/agent;
- arbitrary user-supplied normalization, enrichment, grouping, or correlation code.

Deferral means no placeholder cmdlets, configuration keys, compatibility shims, or implied licensing contract are reserved now.

## Future monitoring candidates

After the first security slice is stable, evaluate audit-policy tampering, Security log lifecycle, privileged logon, certificate-services health, NPS authentication, SMB1/hardening, PowerShell logging with explicit privacy/volume rules, and identity-lifecycle summaries backed by event-derived context.

Each candidate must identify its role, provider/channel/event IDs, prerequisites, volume, typed fields, direct/WEC behavior, aggregation questions, and acceptance evidence before it becomes a preset.

## Release acceptance

Before 4.0 publication:

- all supported .NET targets build without warnings;
- .NET 8 and .NET 10 contract suites pass;
- PowerShell 5.1 and 7 import, help, parameters, exports, and representative real-EVTX paths pass from the packed module;
- EventViewerX, EventViewerX.Reporting, and EventViewerX.Storage share one version and pass packed-artifact validation;
- managed and SQLite aggregation parity is proven for pushdown and fallback boundaries;
- generated HTML and Excel aggregation reports are opened and visually inspected;
- no package is published until the reviewed implementation PR is merged and the maintainer separately authorizes publication.
