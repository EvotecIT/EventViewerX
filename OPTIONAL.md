# Optional EventViewerX capabilities

This document turns recurring PSWinReporting requests into optional EventViewerX product decisions. It is not a commitment to implement every item. Each candidate describes the problem, the proposed public experience, where the behavior should live, the important options, and the evidence required before it can be considered complete.

The design is based on the current EventViewerX `master` architecture:

- [`EventTypeCatalog`](Sources/EventViewerX/EventTypeCatalog.cs) owns built-in event definitions, source channels, event IDs, fields, and composite types.
- [`EventLogProbe`](Sources/EventViewerX/EventLogProbe.cs) owns bounded event-log connectivity and readability checks.
- [`CollectorSubscriptionManager`](Sources/EventViewerX/CollectorSubscriptionManager.Runtime.cs) owns Windows Event Collector configuration and runtime readiness.
- [`EventReportRequest`](Sources/EventViewerX.Reporting/EventReportRequest.cs) and [`EventReportEngine`](Sources/EventViewerX.Reporting/EventReportEngine.cs) own query-to-report orchestration and source coverage.
- [`EventReportPresentationProjection`](Sources/EventViewerX.Reporting/EventReportPresentationProjection.cs) and the format-specific renderers own presentation.
- [`EventStore`](Sources/EventViewerX.Storage/EventStore.cs) owns the current local SQLite history, checkpoint, and summary behavior.
- The internal [`NamedEventsTimelineQueryExecutor`](Sources/EventViewerX/Reports/Correlation/NamedEventsTimelineQueryExecutor.cs) already owns useful correlation primitives and should be promoted or refactored rather than replaced.

## How to use this document

For every candidate, choose one decision:

- **Adopt**: include it in the EventViewerX roadmap.
- **Defer**: keep the design but do not schedule implementation.
- **Reject**: remove the candidate rather than leaving a permanent ambiguous backlog item.

The decision field records the current product direction. Relative cost is architectural breadth, not a calendar estimate. Proposed command, parameter, type, and package names are concrete enough to evaluate, but they remain design candidates until their implementation PR establishes the public contract.

| ID | Candidate | Primary value | Relative cost | Recommended order | Decision |
| --- | --- | --- | --- | --- | --- |
| 1 | AD reporting readiness and onboarding | Makes the product deployable without reverse-engineering Windows prerequisites | Medium | 1 | Adopt |
| 2 | Active Directory target discovery | Removes manual DC inventories while keeping scope visible | Medium | 1 | Adopt, opt-in only |
| 3 | Typed enrichment and GPO resolution | Converts identifiers into useful names without report-specific lookups | Medium | 2 | Adopt, compiled providers only |
| 4 | AD value normalization | Turns raw audit values into understandable data without introducing a general incident model | Medium | 3 | Adopt normalization; defer incidents |
| 5 | Semantic duplicate grouping | Shows one logical occurrence without discarding its source observations | Medium | 3 | Adopt, non-destructive |
| 6 | Typed aggregations and charts | Answers top-N, trend, and rate questions consistently across outputs | Medium | 4 | Adopt |
| 7 | Notification policies and routing | Delivers the right events to the right destination without embedding transport logic in queries | High | 5 | Defer pending TeamsX transport work |
| 8 | Branding and conditional presentation | Makes reports recognizable and highlights important rows consistently | Medium | 5 | Defer pending product/licensing decision |
| 9 | Provider-neutral central storage | Supports shared retention and reporting across collectors and operators | High | 6 | Defer pending product/licensing decision |

### Historical requests not carried forward as new capabilities

Several open PSWinReporting issues already have a current EventViewerX owner and should be closed with migration examples rather than redesigned again:

- source coverage and per-target failure reporting ([#17](https://github.com/EvotecIT/EventViewerX/issues/17));
- nested UserData and embedded key/value payload parsing ([#37](https://github.com/EvotecIT/EventViewerX/issues/37), [#38](https://github.com/EvotecIT/EventViewerX/issues/38));
- typed include/exclude predicates ([#42](https://github.com/EvotecIT/EventViewerX/issues/42));
- NTLMv1 detection ([#69](https://github.com/EvotecIT/EventViewerX/issues/69));
- ForwardedEvents and WEC querying ([#71](https://github.com/EvotecIT/EventViewerX/issues/71)).

The old V2 SMTP configuration failure in [#54](https://github.com/EvotecIT/EventViewerX/issues/54) is not a reason to restore the V2 configuration model. Capability 7 carries forward the still-useful zero-result and routing requirements on top of the current message and SMTP path. The dependency errors in [#86](https://github.com/EvotecIT/EventViewerX/issues/86) belong to the frozen PSWinReporting release and do not require a new EventViewerX feature.

## Shared design rules

These rules apply to all nine candidates.

1. The compiled EventViewerX engine owns discovery, validation, normalization, correlation, aggregation, and storage contracts. PowerShell cmdlets and `evx.exe` translate user input into those contracts.
2. PowerShell 5.1 and PowerShell 7 expose the same parameter names, output types, error classifications, and serialization shape.
3. Read-only commands do not silently configure audit policy, enable channels, create subscriptions, register scheduled tasks, migrate databases, or send test messages.
4. Partial failure is data. A successful domain, source, enrichment provider, or notification route remains usable when another independent target fails.
5. Raw evidence is retained. Normalized, enriched, correlated, or deduplicated views never destroy the source event identity.
6. Portable JSON uses named, validated capabilities. It never contains arbitrary PowerShell or C# code.
7. Secrets are referenced through credentials, environment variables, SecretManagement, managed identity, or another explicit secret provider. They are not embedded in definitions or report profiles.
8. Every bounded operation exposes timeout, cancellation, truncation, and incomplete-result information.
9. HTML, email, Excel, CSV, CLI JSON, and PowerShell objects consume shared result models rather than recomputing product rules independently.
10. Omitting a target always means the local machine. Domain, forest, collector, and trust expansion are explicit opt-in operations.
11. Directory or SYSVOL access is narrowly keyed by identifiers already present in selected events or by an explicit readiness check. EventViewerX does not turn an event query into a general AD/GPO infrastructure inventory.

## Public surface budget

The adopted work should extend the existing `Get-EVXEvent`, `Show-EVXEvent`, `New-EVXFilter`, watcher, collector, and report surfaces. It should not create one cmdlet per check, resolver, normalizer, occurrence policy, aggregation, or chart.

The current design permits at most these new operator-facing commands for capabilities 1-6:

- `Get-EVXTarget` for inspecting the same explicit target resolver used by queries;
- `Get-EVXRequirement` for inspecting generated event requirements;
- `Test-EVXReadiness` for scenario-aware diagnostics;
- `Measure-EVXEvent` for pipeline aggregation when `Show-EVXEvent -GroupBy` is not the appropriate entry point.

Everything else is an option on an existing command, a typed engine API, or an internal service. A proposed fifth cmdlet requires a concrete workflow that cannot remain coherent on these surfaces.

## Dependency map

```text
AD target discovery ---------> readiness ---------> direct/WEC tutorials
         |                         |
         +-------------------------+-------> queries and reports

identity canonicalization --> typed enrichment --> value normalization
             |                                         |
             +-----------------------------------------+--> semantic duplicate groups
                                                               |
                                                               +--> aggregations

occurrence groups + aggregations -----> notification policies -----> transports
                   |                              |
                   +------------------------------+-----> presentation

all query/report results -------------------------------------> storage
```

The map is an implementation dependency, not a requirement to adopt everything. For example, SQLite reporting remains useful without central SQL Server storage.

## 1. AD reporting readiness and onboarding

**Related issues:** [#5](https://github.com/EvotecIT/EventViewerX/issues/5), [#61](https://github.com/EvotecIT/EventViewerX/issues/61), [#79](https://github.com/EvotecIT/EventViewerX/issues/79), [#84](https://github.com/EvotecIT/EventViewerX/issues/84)

**Decision:** Adopt.

### What this gives us

An operator can select the report they want, run one diagnostic, and see whether target discovery, Windows Event Log access, required channels, audit policy, WEC, storage, delivery, and scheduling are ready. The result explains what is proven, what is missing, and what could not be verified.

This addresses the largest onboarding problem in the old tracker: users knew which script to run but did not know what Windows had to record or permit first.

### Proposed public experience

`Test-EVXReadiness` is the PowerShell entry point. `evx doctor` exposes the same engine to scheduled or non-PowerShell environments.

```powershell
# Check a complete daily AD changes workflow across the current forest.
Test-EVXReadiness `
    -Scenario DailyActiveDirectoryReport `
    -ActiveDirectory CurrentForest `
    -OutputPath C:\Reports\EventViewerX

# Check only the requirements for selected typed events.
Test-EVXReadiness `
    -Type ADUserLockouts, ADGroupMembershipChange `
    -ActiveDirectory CurrentDomain

# Validate an existing collector without changing it.
Test-EVXReadiness `
    -Scenario ForwardedActiveDirectoryReport `
    -Collector WEC01 `
    -SubscriptionName 'EventViewerX-AD' `
    -ExpectedSource DC01, DC02
```

```text
evx doctor --scenario daily-ad-report --active-directory current-forest
evx doctor --type ADUserLockouts,ADGroupMembershipChange --collector WEC01
```

The scenario is a convenience profile, not a second requirements database. It resolves to event types and optional workflow checks.

Initial scenarios should be:

- `DirectActiveDirectoryReport`
- `ForwardedActiveDirectoryReport`
- `DailyActiveDirectoryReport`
- `AccountLockoutMonitoring`
- `GroupPolicyMonitoring`
- `AuthenticationMonitoring`
- `CustomDefinition`

### Result contract

The engine should return an `EventReadinessReport` containing ordered `EventReadinessCheckResult` records.

| Property | Purpose |
| --- | --- |
| `Scenario` | Effective scenario, if one was selected |
| `RequestedTypes` | Leaf and composite event types requested by the operator |
| `TargetDiscovery` | Complete target discovery result from capability 2 |
| `Checks` | Ordered check results |
| `RequiredFailures` | Required checks that failed |
| `UnknownRequiredChecks` | Required checks that could not be proven |
| `IsReady` | True only when every required check passed |
| `IsComplete` | False when any required evidence remained unknown or a target was skipped |
| `Duration` | Total bounded execution time |

Each check result should include:

- target, domain, forest, collector, or local host identity;
- layer and check name;
- `Pass`, `Warning`, `Fail`, `Unknown`, or `Skipped` status;
- a separate structured classification such as `AccessDenied`, `Timeout`,
  `Unavailable`, `Missing`, `InvalidConfiguration`, or `NoEvidence` when the
  status alone does not explain why evidence is unavailable;
- whether the check is required for the selected workflow;
- a short evidence statement;
- an actionable remediation statement;
- structured evidence safe for JSON serialization;
- duration and timeout classification.

`Unknown` must remain distinct from `Pass`. For example, reading a recent 5136 event proves that auditing produced at least one event; it does not prove the current effective audit policy on every DC.

The engine should attempt every safe check that the current identity can perform. Missing privileges narrow the evidence and produce `Unknown` with an `AccessDenied` classification; `AccessDenied` is not a sixth status. Permission failures do not stop unrelated checks. A remediation view may show the exact policy, permission, firewall, or service change an administrator could apply, but readiness does not apply it.

### Checks

The check engine should support these layers:

#### Runtime and package

- Windows platform and supported architecture
- PowerShell 5.1 or supported PowerShell 7 runtime
- module import and native dependency load
- required output directories and atomic-write capability
- SQLite native asset load when local storage is requested
- requested output directory and renderer dependencies

#### Target discovery

- current domain/forest resolution
- requested domain and forest reachability
- trust traversal results
- domain controller role, site, writable/read-only, and DNS identity
- duplicate target elimination
- per-domain failures and stable target-set fingerprint

#### Event Log transport

- name resolution and bounded RPC endpoint probe
- Event Log session creation with selected authentication
- required channel existence, enablement, readability, retention mode, and size
- a bounded native query for the selected source/event IDs
- access denied, missing log, host unavailable, timeout, invalid query, and no-event classifications

The existing `EventLogProbe`, session manager, channel-policy service, and query planner should provide these checks. The readiness engine composes them; it does not duplicate native event-reading logic.

#### Audit policy

Event requirements must distinguish three evidence levels:

- **Configured**: a policy source such as a GPO says the subcategory should be enabled.
- **Effective**: the target reports the effective audit subcategory state.
- **Observed**: matching audit events were found in the requested time window.

These levels must never be substituted for one another. Effective policy should use a culture-independent Windows API where available. A remote effective-policy check requires an explicit remote-execution adapter; when that adapter is unavailable, the status is `Unknown`, not inferred from GPO or historical events.

Audit checks should report success/failure settings separately because some event types require success auditing, failure auditing, or both.

#### Windows Event Collector

- collector service installation, state, and start mode
- WinRM service and listener readiness
- `ForwardedEvents` existence and enablement
- subscription existence, enabled state, query definition, delivery mode, content format, heartbeat interval, and source list
- per-source runtime state, last heartbeat, processed events, and Windows error code
- an explicit or discovered expected-source set compared with the runtime
  source/heartbeat set, including sources that never enrolled
- whether the selected EventViewerX types are covered by the subscription query
- whether forwarded payloads retain the fields required by typed projection

The existing collector readiness and runtime models remain the owners for Windows state. The new report adds scenario-aware interpretation. A source-initiated subscription's SDDL says who may enroll; it is not evidence that every expected DC actually enrolled. Collector-only readiness therefore requires `-ExpectedSource`, a pre-resolved target set, or explicit AD discovery when source completeness matters. Without one of those inputs, source coverage is `Unknown` even when every returned runtime source is healthy.

#### Scheduling

- executable/module path exists for the intended account
- command line parses and referenced definition/profile files exist
- output, checkpoint, outbox, and store paths are writable by the intended account
- overlapping execution policy is declared
- task identity, logon mode, working directory, exit-code handling, and timeout are visible
- a previously registered task can be inspected when `-ScheduledTaskName` is supplied

The adopted scope documents and validates a supplied task definition; it does not add a task-registration cmdlet. Registration can be reconsidered only when the existing command surfaces cannot provide a complete scheduled-report experience without custom scripting.

Delivery-profile, transport, and test-message checks belong to deferred capability 7 and are not part of the adopted readiness implementation.

### Requirements as source data

`EventTypeDefinition` should gain a requirements collection or link to an `EventRequirementDescriptor` registry. Composite definitions union the requirements of their leaf definitions.

A requirement descriptor should cover:

- channel and provider;
- event IDs;
- audit subcategory identifiers and success/failure requirements;
- supported Windows versions when behavior differs;
- permissions or privileges;
- WEC content requirements;
- known event-volume or retention considerations;
- links to authoritative Windows documentation.

`Get-EVXRequirement -Type ActiveDirectoryChanges` exposes the same metadata used by readiness and documentation. This avoids a hand-maintained audit-policy table drifting away from actual event rules.

### Onboarding documentation

The documentation should contain two complete paths:

1. **Direct collection for a small environment**
   - choose `CurrentDomain` or `CurrentForest`;
   - run readiness;
   - configure/verify audit policy;
   - create one HTML/Excel/email report;
   - schedule the report with checkpoint or store state;
   - interpret coverage and incomplete results.

2. **WEC collection for production**
   - initialize and validate the collector;
   - create a subscription from EventViewerX types;
   - validate every source heartbeat;
   - query `ForwardedEvents` through `-Collector`;
   - schedule a daily report and preserve state;
   - troubleshoot missing or delayed sources.

The requirements matrix should be generated from `EventRequirementDescriptor`. The tutorial remains human-authored and uses the installed public module and CLI, not repository build paths.

### Options and tradeoffs

- `IsReady` remains false whenever a required result is `Fail` or `Unknown`.
  `-Strict` adds an observable automation boundary: after composing the complete
  report, PowerShell raises the terminating error ID
  `EventViewerX.ReadinessIncomplete` and the CLI exits with code 2 when any
  required result is `Unknown`. Without `-Strict`, the report is returned for
  interactive assessment without claiming readiness; required `Fail` results
  retain the command's normal failed-gate behavior in both modes.
- `-Check` and `-SkipCheck` allow focused diagnosis, but the report must state that it is partial.
- Remote effective audit-policy inspection provides stronger proof but requires a separate execution channel and broader permissions.
- A task-registration helper improves onboarding but introduces a mutating Windows boundary; it should follow the diagnostic work, not be bundled into it.

### Acceptance gate

- The same selected workflow produces equivalent structured results in PowerShell 5.1, PowerShell 7, and CLI JSON.
- A mixed target set proves successful DCs while classifying inaccessible, missing-channel, and timeout targets independently.
- Audit-policy state distinguishes configured, effective, observed, and unknown evidence.
- WEC readiness includes per-source runtime and heartbeat evidence.
- The direct and WEC tutorials work from a clean installed package without source-checkout paths.
- The command performs no mutation and sends no message unless a separate explicit operation is invoked.

## 2. Active Directory target discovery

**Related issue:** [#89](https://github.com/EvotecIT/EventViewerX/issues/89)

**Decision:** Adopt as an explicit opt-in target mode. The default remains the local machine.

### What this gives us

Operators no longer maintain a hand-written DC list for common AD reports. Unlike the old implicit `DetectDC` behavior, the effective domain, forest, trust path, selected roles, and failures remain observable and serializable.

### Proposed public experience

The common path is built directly into query, report, readiness, watcher, and collector-definition commands. It is never selected implicitly:

```powershell
Get-EVXEvent -Type ActiveDirectoryChanges `
    -ActiveDirectory CurrentDomain

Show-EVXEvent -Type ActiveDirectoryChanges `
    -ActiveDirectory CurrentForest `
    -SiteName Warsaw, Berlin

Show-EVXEvent -Type ADUserLockouts `
    -ActiveDirectory CurrentForest `
    -DomainControllerRole PdcEmulator
```

Named domains and forests are explicit scopes:

```powershell
Get-EVXEvent -Type ActiveDirectoryChanges `
    -ActiveDirectory Domain `
    -DomainName corp.example.com

Get-EVXEvent -Type ActiveDirectoryChanges `
    -ActiveDirectory Forest `
    -ForestName example.com `
    -IncludeTrust Forest `
    -TrustDirection Outbound
```

The inspectable building block returns the complete batch result through the deliberately generic `Get-EVXTarget` command rather than adding one resolver command for every target family:

```powershell
$targetSet = Get-EVXTarget `
    -ActiveDirectory CurrentForest `
    -DomainControllerRole Any `
    -ReadOnlyDomainController Include

$targetSet.Targets
$targetSet.Domains
$targetSet.Diagnostics

Get-EVXEvent -Type ActiveDirectoryChanges -TargetSet $targetSet
```

### Scope model

`ActiveDirectoryTargetScope` should contain:

- `CurrentDomain`
- `CurrentForest`
- `Domain`, requiring `-DomainName`
- `Forest`, requiring `-ForestName`

Trust traversal is a separate concept because the current forest already contains all of its child domains. `-IncludeTrust` should accept `Forest`, `External`, or `All`; it defaults to none. `-TrustDirection` should accept `Inbound`, `Outbound`, `Bidirectional`, or `Any` and default to `Any` after trust traversal has been explicitly enabled. Every trust edge is normalized as `FromScope -> ToPartner`, where `FromScope` is the domain or forest whose trust API was enumerated. Direction is always interpreted from that `FromScope`: `Outbound` means the enumerated source trusts the partner, and `Inbound` means the partner trusts the enumerated source. Filtering occurs only after that orientation is recorded, including at deeper traversal levels, so enumeration order cannot reverse the meaning. The result reports both endpoints, the normalized direction, and the trust path, and proves Event Log access separately instead of predicting access from direction alone.

Target filtering should use orthogonal parameters:

- `-DomainControllerRole Any|PdcEmulator|GlobalCatalog`
- `-ReadOnlyDomainController Include|Exclude|Only`
- `-SiteName <string[]>`
- `-DomainName <string[]>`
- `-ForestName <string[]>`
- `-DiscoveryTimeout <TimeSpan>` for the whole target-resolution operation
- `-DomainTimeout <TimeSpan>` for one directory/domain operation
- `-MaximumDomainCount <int>`
- `-MaximumTargetCount <int>`
- `-MaximumTrustDepth <int>` when trust expansion is enabled

Additional FSMO roles can be added to the role enum if a real event-reporting workflow requires them. They should not become separate switches.

### Credentials and authentication

Directory discovery and Event Log access are different security boundaries:

- `-DirectoryCredential` is used for directory/forest discovery.
- `-Credential` is used for Windows Event Log sessions.
- If only `-Credential` is supplied, it may be used for discovery as a documented convenience when the selected discovery provider supports it.
- Credentials are never serialized into a target set, checkpoint key, report, or diagnostic.

### Result contract

`ActiveDirectoryTargetDiscoveryResult` should contain:

- normalized request;
- discovered forests and domains;
- selected domain controllers;
- domain/forest/trust diagnostics;
- duplicate and exclusion counts;
- duration, cancellation, and truncation state;
- a stable fingerprint of scope and selected target identities.

Each `ActiveDirectoryEventTarget` should include:

- DNS host name and optional NetBIOS name;
- domain and forest DNS names;
- site;
- writable/read-only state;
- global catalog and known role flags;
- discovery source and trust path;
- stable normalized target identity.

Discovery diagnostics remain separate from `EventReportCoverage`. A domain can fail before a machine/log pair exists. Reports should expose `TargetDiscovery` and `Coverage` as separate sections and render both.

### Query integration

`EventReportRequest` and typed query requests should accept one `TargetSelection` union:

- local/default;
- explicit machines;
- collector machines;
- offline files;
- AD discovery request;
- pre-resolved target set.

This replaces growing collections of mutually exclusive nullable properties inside new code while preserving current public parameters for compatibility. Validation occurs once in the target-selection owner.

A stable consumer/query-profile identity identifies the scheduled job. It is
derived from an explicit profile name or a canonical fingerprint of the event
types/definition, typed predicate, source-selection semantics, and other
record-eligibility semantics. Credentials, resolved target membership,
run-specific timestamps calculated from a relative window, concurrency, and
scan/page caps are excluded. The canonical relative-window preset or duration
itself is included, so changing `Last1Hour` to `Last24Hours` changes identity
without making every scheduled run a new profile. Fixed absolute start/end
bounds are also included because changing them changes the eligible historical
record set. When an explicit profile name is used, the store also retains the
canonical semantic fingerprint and rejects accidental reuse of that name for a
different filter or fixed bound unless the operator performs an explicit
checkpoint reset/migration that records the old and new fingerprints.
Beneath that identity, durable progress is partitioned by the
normalized discovery request plus each stable target/channel identity. Two
jobs that happen to discover the same DCs can therefore never advance one
another's checkpoints.

The resolved target fingerprint is recorded as drift evidence, not placed in
every checkpoint key. A newly discovered target starts without a high-water
mark; an unchanged target reuses its own checkpoint even when another DC is
added, removed, or temporarily unreachable. Retired target checkpoints remain
available for a bounded retention period so a transient discovery failure does
not force replay when the target returns.

Long-running watchers use a bounded `TargetRefreshInterval` rather than treating
AD discovery as a permanent startup snapshot. Each refresh publishes the old
and new fingerprints plus added, retained, removed, and failed targets. New
targets attach from their retained per-profile checkpoint or the watcher's
explicit initial window. Removed targets stop accepting new work only after
their in-flight read is drained; their checkpoints are retained. A partial
rediscovery does not silently remove previously healthy targets: they remain
active but stale until a complete refresh confirms removal. Operators may set
`-RequireCompleteDiscovery` to stop the watcher instead. Refresh deadlines,
limits, failures, and reconciliation actions are observable in watcher health
and summary output.

### Failure and safety behavior

- No target parameters still means the local machine.
- `CurrentDomain` and `CurrentForest` are never defaults, even for AD composite event types.
- Trusts are never traversed without `-IncludeTrust`.
- One failed domain does not discard successful domains unless `-RequireCompleteDiscovery` is selected.
- `-RequireCompleteDiscovery` terminates before event querying if any required discovery scope failed.
- Default discovery is bounded by per-domain timeout and maximum concurrency.
- Every discovery also has an overall deadline plus maximum domain, target,
  and trust-depth limits. Reaching any limit stops expansion, retains completed
  domains, and reports the exact truncation reason and unvisited frontier.
- A target is deduplicated by normalized host identity even when discovered through several trust paths.
- Discovery reports the identity/context used without exposing credentials.

### Acceptance gate

- Current-domain, multidomain current-forest, named-domain, and named-forest scopes produce deterministic target sets.
- Site, PDC, global catalog, writable, and RODC filters compose predictably.
- Trust traversal detects loops and duplicate paths.
- Large trust graphs stop deterministically at the declared overall deadline,
  domain/target limits, or trust-depth limit and return partial/truncated data.
- Per-domain timeout and access failures remain visible beside successful targets.
- `Get-EVXEvent`, `Show-EVXEvent`, readiness, and CLI use one core resolver.
- PowerShell 5.1 and PowerShell 7 return the same target/result shapes.
- Long-running watcher refresh adds and removes targets deterministically,
  preserves per-profile progress, and never treats a failed refresh as proof
  that a previously healthy target was removed.

## 3. Typed enrichment and GPO resolution

**Related issues:** [#83](https://github.com/EvotecIT/EventViewerX/issues/83), [#87](https://github.com/EvotecIT/EventViewerX/issues/87)

**Decision:** Adopt built-in compiled enrichment. Do not expose custom scripts, process-local PowerShell providers, or arbitrary user logic.

### What this gives us

Identifiers such as GPO GUIDs, SIDs, IP addresses, distinguished names, and service identifiers can be enriched consistently after event projection. Reports stop embedding one-off directory lookups. Built-in definitions and declarative custom event definitions may select only enrichment capabilities compiled and shipped by EventViewerX.

### Proposed model

Promote the current DNS-only enrichment path into an ordered `EventEnrichmentPipeline`.

```csharp
public interface IEventEnrichmentProvider {
    string Name { get; }
    ValueTask<EventEnrichmentOutcome> EnrichAsync(
        EventTypeRecord record,
        EventEnrichmentContext context,
        CancellationToken cancellationToken);
}
```

The context owns bounded lookup services, cache, credentials, target metadata, and diagnostics. Providers do not open their own unbounded directory clients or hide failures in console output.

Before provider selection or cache lookup, a shared identity-canonicalization
stage parses SID, GUID, distinguished-name, DNS/IP, and other provider-key
representations into stable typed identities while retaining the raw value.
Enrichment providers consume only those canonical identities and never carry
their own competing normalization. Capability 4 then performs the broader
display/value normalization of event fields and enriched values. Thus identity
canonicalization precedes enrichment even though user-facing value
normalization follows it.

Initial built-in providers should be:

- `ReverseDns`
- `ActiveDirectoryIdentity`
- `GroupPolicy`

The GPO provider resolves GUID/DN to display name, domain, status, and canonical identity for every applicable GPO record, not only link events. A lookup is keyed by a GPO identifier already present in the selected event. It does not enumerate every GPO, OU, link, or SYSVOL object as a side effect of reading events.

### Public experience

```powershell
Get-EVXEvent -Type GroupPolicyActivity `
    -ActiveDirectory CurrentForest `
    -Enrichment GroupPolicy

Get-EVXEvent -Type ActiveDirectoryAuthentication `
    -ActiveDirectory CurrentDomain `
    -Enrichment ReverseDns, ActiveDirectoryIdentity `
    -EnrichmentFailure BestEffort
```

Portable custom definitions may reference registered provider names and destination fields:

```json
{
  "name": "CustomGpoChange",
  "enrichment": [
    {
      "provider": "GroupPolicy",
      "sourceField": "GroupPolicyId",
      "output": {
        "displayName": "GroupPolicyDisplayName",
        "domain": "GroupPolicyDomain"
      }
    }
  ]
}
```

Unknown providers make definition validation fail before querying. Output
names are validated case-insensitively before querying and cannot collide with
raw source fields, common EventViewerX fields, normalized fields, or another
enrichment output. Definitions never overwrite source evidence.

### Custom code boundary

Event definitions cannot register or execute scripts, delegates, expressions with method calls, or process-local providers. EventViewerX owns the complete supported enrichment catalog and its stable output fields.

Compiled third-party provider registration is also outside the adopted scope. It can be reconsidered later if a real reusable provider cannot be shipped in EventViewerX itself. Ordinary PowerShell pipeline post-processing remains possible because PowerShell objects are composable, but it is not an EventViewerX definition or enrichment feature and does not alter EventViewerX report schema.

Existing projection-time directory calls—including the current GPO lookups in
`ADGroupPolicyEdits` and `ADGroupPolicyLinks`—must be removed from record
constructors. They route exclusively through the bounded `GroupPolicy`
provider, after `LiveLookup`, credential, timeout, cache, and authorization
policy are known. `LiveLookup Never` therefore performs no directory call with
either the process identity or an implicit credential.

### Persistent historical context

Some objects cannot be resolved after the event occurs. A deleted GPO is the clearest example: its deletion event may contain a GUID or DN, while a later directory lookup can no longer recover its display name. An in-memory TTL cache is therefore insufficient.

The enrichment engine should accept an `IEventContextStore` with an in-memory implementation and an optional persistent SQLite implementation. The context store records historical facts, not copies of directory infrastructure:

- normalized object identity, object class, forest/domain context, and known aliases;
- first-seen, valid-from, valid-to, last-observed, and deletion timestamps;
- value provenance such as event payload, targeted directory lookup, or a compiled consumer-provided evidence record;
- source event identity or query evidence;
- provider/schema version and confidence reason;
- current, historical, deleted, ambiguous, or unknown state.

Resolution always prefers sufficient data already present in the event. After
that, lookup order depends on `LiveLookup`:

- `Never` uses authorized historical context valid at the event time and does
  not contact the directory;
- `WhenMissing` uses authorized historical context first and performs a
  narrowly targeted live lookup only when context is insufficient;
- `Always` performs the narrowly targeted live lookup before accepting stored
  context, while retaining the historical value separately so a current rename
  cannot rewrite what was true at the event time.

Newly proven context is persisted before the result is returned. An unresolved
lookup returns the raw identifier plus a structured reason.

Facts are reconciled by their effective event time, never by ingestion order.
The store inserts or splits validity intervals so an older rename arriving late
cannot overwrite a newer name, and a newest-first historical read produces the
same timeline as an oldest-first read. Proven non-overlapping facts form
ordered intervals. Conflicting facts whose time/provenance cannot establish an
order remain side-by-side as `Ambiguous`; insertion order is not a tiebreaker.
Event-derived deletion/rename evidence and targeted live evidence retain their
separate timestamps and provenance, so a current lookup can close or extend an
interval without rewriting the fact that applied at an earlier event time.

For a deletion, historical context may supply the last known name while the event remains marked deleted. The output must distinguish `NameAtEventTime`, `LastKnownName`, and a current live name; it must not present stale context as a current directory fact.

The context store is useful beyond GPOs: deleted users, groups, computers, renamed objects, SID/name history, certificate template identities, and provider metadata can use the same validity/provenance model when a concrete event type requires it.

TestimoX remains the owner of broad AD/SYSVOL state monitoring and comparison. EventViewerX may expose a compiled, versioned evidence-import contract so TestimoX can seed narrowly relevant historical facts, but EventViewerX does not invoke a TestimoX scan or enumerate the environment during an event query.

### Options

- `-Enrichment <name[]>`
- `-EnrichmentFailure BestEffort|Required`
- `-EnrichmentTimeout <TimeSpan>`
- `-EnrichmentConcurrency <int>`
- `-DirectoryCredential <PSCredential>`
- provider-specific configuration through validated option objects, not arbitrary hashtables
- `-ContextStore <path or typed store>` when historical enrichment should survive the process
- `-LiveLookup Never|WhenMissing|Always`, defaulting to `WhenMissing` for explicitly selected enrichment

Records retain original identifier fields. Enriched values use separate properties and include provider/provenance diagnostics when requested.

### Caching and failure behavior

- cache keys include provider, normalized input, directory/forest context,
  relevant configuration, and a non-secret authorization-context identity;
- authorization context uses an effective-principal/security-context
  fingerprint or a request-isolated cache partition. It never stores a
  password, token, or reversible credential value;
- positive, negative, not-found, and access-denied entries are never reused
  across authorization contexts;
- positive and negative cache lifetimes are bounded independently;
- persistent facts retain validity intervals and provenance rather than expiring solely by TTL;
- lookup-derived persistent facts carry the same non-secret authorization-
  context partition or security label as the lookup that proved them. They are
  never returned to another context merely because both callers share a store
  path. Facts proven directly by an event payload or explicitly imported as
  shareable evidence retain their own provenance and disclosure policy;
- caller cancellation stops queued work;
- result ordering remains event ordering even when lookups overlap;
- `BestEffort` records enrichment failures without removing events;
- `Required` marks the affected report incomplete and can stop before notification delivery.

### Acceptance gate

- GPO display names are consistent across link, edit, detailed change, and audit records.
- Repeated identifiers use the bounded shared cache.
- A privileged lookup cannot populate a result later returned to a less
  privileged caller, and a restricted caller cannot poison another caller's
  negative cache.
- Lookup timeout, access denied, not found, ambiguous, and provider failure are distinct outcomes.
- Raw identifiers are always preserved.
- Custom JSON cannot execute code.
- A deleted GPO can resolve a last-known name from context without claiming that the GPO still exists.
- The same timestamped fact set produces the same validity intervals when
  ingested oldest-first, newest-first, or with late WEC arrivals; unresolved
  same-time conflicts remain explicitly ambiguous.
- Live lookups are targeted by event identity and never enumerate the AD or SYSVOL estate.
- PowerShell 5.1, PowerShell 7, CLI, and C# consume the same provider pipeline.

## 4. AD value normalization

**Related issue:** [#55](https://github.com/EvotecIT/EventViewerX/issues/55)

**Decision:** Adopt deterministic value normalization. Defer incidents and general multi-event correlation until their user model and boundaries are understood.

### What this gives us

Reports can explain individual event values instead of presenting raw values such as `%%14672`, FILETIME integers, UAC masks, and OIDs. This work improves each event record without claiming that several records form one higher-level incident.

### Normalization model

Normalization is deterministic and local; enrichment performs external lookup. Keep those stages separate.

```csharp
public interface IEventValueNormalizer {
    string Name { get; }
    bool CanNormalize(EventValueContext context);
    EventNormalizedValue Normalize(EventValueContext context);
}
```

`EventNormalizedValue` should retain:

- raw value;
- normalized typed value;
- display value;
- stable value kind;
- normalizer name/version;
- warnings or loss indicators.

The registry should select by provider, event ID/type, field/attribute name, and declared value kind. A generic numeric conversion must not guess that every large integer is FILETIME.

Initial normalizers should cover:

- Windows audit resource identifiers;
- AD operation types;
- UAC bit masks and old/new UAC differences;
- AD FILETIME values, including sentinel values;
- SID, GUID, DN, and OID canonical formatting;
- common SPN and multivalue rendering;
- selected Exchange AD attributes requested by real captured events.

Unknown values remain visible as raw values. A normalizer never replaces them with an empty string.

### Correlation model

**Deferred design, not part of the current implementation goal.** “Incident” may mean an account lifecycle, one GPO transaction, one authentication sequence, or a time-window grouping, and those meanings should not be hidden behind one premature abstraction. No public incident cmdlet or schema is reserved yet.

The existing internal named-event timeline/correlation implementation should become the starting owner. It already groups typed events by stable dimensions and tracks truncation and target failures. It should be promoted into a public correlation engine rather than copied into reporting.

`EventIncidentDefinition` describes:

- included event types;
- correlation keys;
- event-time window;
- ordering policy;
- start, continuation, and terminal events;
- optional and required steps;
- summary projection;
- late-arrival allowance;
- maximum open incidents and eviction behavior.

Possible future incident types to evaluate with real captured evidence are:

- `ActiveDirectoryAccountLifecycle`
- `GroupPolicyChangeTransaction`
- `AccountLockout`

```powershell
Get-EVXEvent -Type ActiveDirectoryChanges `
    -ActiveDirectory CurrentForest |
    ConvertTo-EVXIncident -IncidentType ActiveDirectoryAccountLifecycle `
        -Window 00:05:00

Show-EVXEvent -FromStore C:\EVX\events.db `
    -IncidentType GroupPolicyChangeTransaction `
    -IncludeSourceEvent
```

The command shape above is illustrative only. It is not an adopted API. If correlation is adopted later, reporting and storage should call the same engine directly.

### Incident result

An `EventIncident` should include:

- stable incident ID and definition;
- correlation keys and normalized identities;
- first/last event times;
- state such as complete, partial, timed out, or ambiguous;
- summary and important field changes;
- ordered source-event identities;
- contributing machines and collectors;
- completeness, truncation, and confidence information.

Confidence must be evidence-based, not a vague score. Prefer named levels with reasons such as `ExactKeyAndCompleteSequence`, `ExactKeyPartialSequence`, or `TimeWindowOnly`.

### Streaming versus stored correlation

- A bounded report can correlate events already present in that report.
- A watcher can keep bounded in-memory open incidents and flush them by terminal event or timeout.
- Correlation across scheduled runs requires durable state. The current SQLite store can hold incident state before central storage is adopted.
- Late WEC arrivals must be supported through a declared lateness window; otherwise reports should mark incidents incomplete rather than silently attaching them to the wrong sequence.

### Acceptance gate

- Raw and normalized values are both available in PowerShell, JSON, CSV, HTML, email, and Excel.
- Culture and UI language do not change stable normalized values.
- Unknown and malformed values retain their raw evidence and an explicit normalization outcome.
- Normalization is selected by event/provider/field context and never guesses a semantic type from magnitude alone.
- Incident/correlation APIs, durable state, and streaming behavior remain out of scope for this delivery.

If incidents are adopted later, their separate acceptance gate must cover preservation of contributing source-event identity, out-of-order and late events, bounded state, eviction, cancellation, truncation, and reuse of the existing correlation implementation.

## 5. Semantic duplicate grouping

**Related issue:** [#50](https://github.com/EvotecIT/EventViewerX/issues/50)

**Decision:** Adopt as non-destructive occurrence grouping. Raw event retrieval and source identity remain unchanged.

### What this gives us

When Windows or a provider supplies a shared causal discriminator, one logical
event observed by multiple sources can appear as one occurrence group while
retaining every audit observation. Where that discriminator is absent—as with
ordinary 4740 DC/PDC lockout records—the observations remain separate or are
shown as an explicitly ambiguous candidate set. This avoids noisy rows only
when the evidence supports it, without weakening forensic identity. It is
deliberately narrower than the deferred incident/correlation design.

### Identity levels

EventViewerX should name three different concepts:

1. **Source identity**: provider, channel, source computer, record ID, and event timestamp.
2. **Transport identity**: the same source event observed directly and through WEC.
3. **Occurrence-group identity**: separate source events that a versioned event-specific policy proves describe one logical occurrence.

The current store already handles transport-level deduplication. Occurrence grouping must be opt-in and rule-specific. `Get-EVXEvent` continues to return source observations; grouping is requested by report, aggregation, or store-query presentation.

### Proposed model

`IEventOccurrencePolicy` creates a normalized candidate key and validates whether two observations can belong to one occurrence group. Each policy has a stable name and version so derived membership can be explained and recomputed after policy improvements.

An account-lockout policy may consider:

- normalized affected account SID/name;
- caller computer when present;
- event type and relevant status;
- source domain;
- a narrow configurable event-time window;
- DC/PDC relationship from target metadata.

These dimensions create candidates only. Final membership requires evidence
that the source observations share one causal occurrence, such as a provider
activity/correlation identifier or another event-specific value proven to be
stable across the DC/PDC observations and distinct across repeated lockouts.
Time proximity, account, caller, and DC/PDC role alone are insufficient. When
Windows does not emit enough evidence to distinguish two repeated lockouts,
the policy preserves the observations as separate or explicitly ambiguous;
it does not guess and undercount them.

```powershell
Show-EVXEvent -Type ADUserLockouts `
    -ActiveDirectory CurrentForest `
    -DuplicateMode Semantic `
    -DuplicateWindow 00:00:10
```

Options:

- `None`: do not group duplicates.
- `Transport`: collapse repeated observations of the exact source event.
- `Semantic`: group policy-matched source events into duplicate sets.

`Semantic` results expose the representative event, observation count, all source identities, machines, record IDs, policy name/version, and match reason.

Best-practice persistence is layered:

- immutable source events remain the authoritative stored evidence;
- occurrence membership is a derived, versioned projection;
- a store may materialize membership for performance, but the materialization is disposable and recomputable;
- changing a policy version never rewrites source identities or silently changes an exported historical artifact;
- report metadata records the policy version used.

### Representative selection

The representative should be deterministic:

1. prefer the record with the richest typed fields;
2. prefer the authoritative role declared by the policy, if applicable;
3. prefer direct source provenance over forwarded provenance only when field completeness is equal;
4. break ties by event time, normalized machine, and record ID.

### Acceptance gate

- Direct plus forwarded copies of one source event remain transport duplicates.
- DC and PDC records become one semantic set only when captured evidence
  contains a validated shared causal discriminator; ordinary 4740 records
  without one remain separate or explicitly ambiguous.
- Repeated real lockouts outside or inside the window are not merged when their semantic keys differ.
- Captures without a shared causal discriminator remain separate or
  explicitly ambiguous even when every candidate dimension and timestamp
  window matches.
- Every source record remains inspectable and exportable.
- Materialized occurrence membership can be deleted and rebuilt without changing source events.
- Grouping behavior is stable across input ordering and PowerShell runtimes.

## 6. Typed aggregations and charts

**Related issue:** [#43](https://github.com/EvotecIT/EventViewerX/issues/43)

**Decision:** Adopt. Aggregations operate on raw observations or explicitly selected occurrence groups; they do not depend on deferred incidents.

### What this gives us

Operators can ask “which accounts lock out most often?”, “which IPs generate failed logons?”, or “how did GPO changes trend by day?” without format-specific scripts. HTML, Excel, email, CLI JSON, and PowerShell all receive the same aggregation result.

### Proposed model

`EventAggregationDefinition` should describe:

- source event types or occurrence groups;
- group-by fields;
- optional time bucket and timezone;
- measures;
- ordering and top-N limit;
- null/unknown handling;
- optional typed predicate;
- display metadata for renderers.

Measures are typed descriptors rather than enum values alone. An
`EventAggregationMeasure` contains the operation, optional semantic field
operand, null policy, comparison/canonicalization policy, and output name.
`Count` has no operand. `DistinctCount` requires an operand and declares
whether null/unknown values participate. `FirstSeen` and `LastSeen` require one
datetime semantic operand and return the minimum or maximum non-null instant;
an empty group is not emitted. `Rate` requires a unit and a deterministic
interval source: either the declared time bucket or the explicit query window.
Its denominator is the full normalized interval duration, not the elapsed time
between the first and last matching event. A zero-length interval is rejected;
an empty non-zero interval produces a zero rate. Bucket boundaries use the
report timezone and the same documented DST gap/overlap rules as grouping.

Initial operations:

- `Count`
- `DistinctCount`
- `FirstSeen`
- `LastSeen`
- `Rate`

`Duration` is deliberately not an initial operation. It is deferred until a
real workflow defines whether it means an occurrence lifetime, two explicit
datetime operands, or another interval, including aggregation and null rules.

```powershell
$lockoutBatch = Get-EVXEvent `
    -Type ADUserLockouts `
    -ActiveDirectory CurrentForest `
    -AsResult

Measure-EVXEvent `
    -InputResult $lockoutBatch `
    -GroupBy UserAffected `
    -Measure Count `
    -Top 10

$failureBatch = Get-EVXEvent `
    -Type ADUserLogonFailed `
    -ActiveDirectory CurrentDomain `
    -AsResult

Measure-EVXEvent `
    -InputResult $failureBatch `
    -GroupBy SourceComputer `
    -Measure @{ Operation = 'DistinctCount'; Field = 'ObjectAffected'; Nulls = 'Exclude' }

Show-EVXEvent -FromStore C:\EVX\events.db `
    -Type ADUserLogonFailed `
    -GroupBy Who, IPAddress `
    -Bucket Day `
    -Measure Count `
    -Top 20
```

Fields are validated against typed definitions before execution. Compatibility
uses a stable semantic field identifier plus compatible value kind,
canonicalization, and comparison semantics; matching property names are not
enough. A composite may use only semantic fields common to every selected leaf
type unless it selects one leaf explicitly or declares missing-value behavior.
For example, two properties named `Who` cannot be aggregated together when
one means an account identity and another means a workstation identity.

Top-N ordering is total, not measure-only. Requested order and measure values
are compared first; ties are broken by the canonical serialized group key using
the declared ordinal Unicode/case policy, with null/unknown ordered by its
explicit bucket token. This secondary order is mandatory even when the caller
specifies only `-Top`, and providers must use the same key bytes.

Time-bucket identity is the pair of UTC start/end instants produced from the
requested local calendar boundary plus the display timezone/offset. During a
fall-back overlap, the repeated local hour is represented by two distinct
offset-qualified buckets. During a spring-forward gap, the nonexistent local
hour produces no bucket. A local calendar day runs from one valid local
midnight to the next; its `Rate` denominator is elapsed UTC duration and is
therefore normally 24 hours but may be 23 or 25. Ambiguous/nonexistent boundary
conversion uses the timezone's ordered offsets and next valid instant,
respectively, and the chosen UTC boundaries are serialized in the result.

### Execution

- In-memory reports use the shared managed aggregation engine.
- Managed execution requires explicit `MaximumGroups` and
  `MaximumDistinctValues` bounds plus a total aggregation-state memory budget.
  Defaults are conservative and scenario-owned; callers may lower them, while
  raising them is explicit. If a complete grouping would exceed any bound, the
  initial engine fails closed with an incomplete diagnostic-only result and no
  aggregate/top-N/`Other` rows. It does not keep the first groups encountered,
  so input order cannot change a partial answer. A caller may rerun with a
  larger explicit bound or a provider that can prove a bounded complete result.
- The initial managed engine does not spill sensitive event-derived state to
  disk implicitly. A future explicit spill mode must use a caller-selected,
  protected store and preserve the same incomplete-result contract.
- Storage providers may push supported group/bucket/measure operations into the database.
- Pushdown must enforce equivalent group/cardinality/result bounds or reject
  the request. It may count cardinality within the same bounded query before
  materializing rows; a provider cannot bypass the managed safety contract.
- Unsupported provider operations fall back to the managed engine only when the bounded read is acceptable.
- `Explain` reports which filters and aggregations were pushed down, which ran in managed code, and whether a cap made the result incomplete.

Aggregation accepts an `EventQueryBatchResult` envelope containing rows,
per-target coverage/failures, truncation, cancellation, and source-query
identity. `Get-EVXEvent -AsResult` and store/report owners produce that envelope.
Plain `-InputObject` rows have `InputCompleteness = Unknown` unless the caller
explicitly supplies a validated completeness descriptor; `EventAggregationResult`
then remains incomplete even when aggregation state itself is bounded. A
failure or timeout on one source can never disappear merely because only the
successful rows flowed through the PowerShell pipeline.

Renderers consume `EventAggregationResult`. Excel and HTML do not independently decide how to group data. Chart type is a presentation choice applied to an already-computed result.

### Presentation options

- table, bar, line, stacked bar, or metric tiles;
- top-N plus optional “Other” bucket. `Other` is a fresh aggregation over the
  union of source or occurrence rows belonging to every discarded group; it is
  never computed by summing discarded aggregate rows. Providers must preserve
  enough state to recompute non-additive measures such as `DistinctCount`,
  `FirstSeen`, and `LastSeen`, fall back to the bounded managed engine, or reject
  `Other` for an unsupported pushdown;
- UTC or declared report timezone;
- stable case/Unicode comparison policy;
- explicit unknown/empty bucket;
- links from an aggregate row back to matching source events when the output supports interaction.

### Acceptance gate

- Account-lockout top-N is identical in PowerShell objects, CLI JSON, HTML, and Excel.
- Unicode account names and case variants follow one documented grouping policy.
- Time buckets handle UTC, timezone conversion, DST gaps, and overlaps deterministically.
- Query coverage plus aggregation truncation are preserved in every result and
  renderer; a plain row array is never silently labeled complete.
- High-cardinality group and distinct-count inputs stop at deterministic state
  bounds and cannot exhaust memory merely because `Top` is small.
- SQLite pushdown and managed fallback produce the same result for the same bounded dataset.
- Top-N `Other` results for additive and non-additive measures equal a fresh
  managed aggregation over the union of discarded source rows.

## 7. Notification policies and routing

**Related issues:** [#7](https://github.com/EvotecIT/EventViewerX/issues/7), [#8](https://github.com/EvotecIT/EventViewerX/issues/8), [#39](https://github.com/EvotecIT/EventViewerX/issues/39), [#54](https://github.com/EvotecIT/EventViewerX/issues/54), [#75](https://github.com/EvotecIT/EventViewerX/issues/75)

**Decision:** Defer. TeamsX is being redesigned to own Teams, Slack, and Discord delivery. Until that transport direction settles, EventViewerX will not introduce a competing notification-policy or adapter system. Users may compose current report/email packages with Mailozaurr themselves.

### What this gives us

EventViewerX can route important incidents to different destinations, suppress empty noise, batch routine changes, and preserve delivery evidence. Querying and report rendering remain independent of SMTP, Teams, Graph, or another transport.

### Ownership

- `EventViewerX.Reporting` creates reports and transport-neutral message packages.
- A notification-policy engine evaluates typed events, incidents, aggregates, and readiness results.
- Transport adapters deliver an immutable message package and return a receipt.
- The CLI may host built-in SMTP because it already depends on Mailozaurr.
- TeamsX/PSTeams, Graph, Slack, or future transports should be optional adapters rather than dependencies of the core event engine.

### Policy model

`EventNotificationPolicy` should contain:

- input type: events, incidents, aggregates, readiness results, or health heartbeat;
- typed condition and optional threshold;
- severity and title template;
- digest window and maximum rows;
- suppression, cooldown, and repeat behavior;
- one or more named routes;
- zero-result behavior;
- failure policy and durable outbox options.

```json
{
  "name": "PrivilegedGroupChanges",
  "input": "Event",
  "types": ["ADGroupMembershipChange"],
  "where": {
    "field": "GroupName",
    "operator": "In",
    "values": ["Domain Admins", "Enterprise Admins"]
  },
  "severity": "High",
  "digestWindow": "00:05:00",
  "routes": ["SecurityEmail", "SecurityTeams"],
  "zeroResult": "Suppress"
}
```

ManagedBy delivery is implemented as a route resolver backed by capability 3. The event remains routable when ManagedBy lookup fails according to an explicit fallback route; it is never silently discarded.

### Public experience

```powershell
Invoke-EVXNotification `
    -InputObject $report `
    -Policy C:\EVX\notification-policy.json `
    -WhatIf

Start-EVXWatcher -Type ActiveDirectoryChanges `
    -ActiveDirectory CurrentForest `
    -NotificationPolicy C:\EVX\notification-policy.json
```

```text
evx notify --report report.json --policy notification-policy.json
evx watch --type ActiveDirectoryChanges --notification-policy notification-policy.json
```

### Zero-result and health behavior

Every policy declares one behavior:

- `Suppress` (recommended default)
- `SendEmptyReport`
- `SendHealthHeartbeat`

A health heartbeat includes target/readiness/coverage state. It must not imply “no changes” when the query was incomplete.

### Delivery reliability

Delivery should use stable notification IDs derived from policy, window, and input identity. The outbox stores pending package, attempts, next retry, and final receipt so a task restart does not send uncontrolled duplicates.

Each `EventNotificationReceipt` includes:

- route and transport;
- notification ID;
- accepted/delivered/failed status according to transport semantics;
- attempt count and timestamps;
- remote message ID when supplied;
- safe error classification;
- input/report fingerprint.

Secrets and response bodies are excluded.

### Options and tradeoffs

- `StopOnRouteFailure` versus independent route delivery;
- immediate versus digest delivery;
- cooldown and escalation;
- fallback route when enrichment-based recipients cannot be resolved;
- at-most-once versus at-least-once retry intent, clearly documented because most external transports cannot provide exactly-once delivery;
- local outbox versus shared store-backed outbox.

### Acceptance gate

- Zero-event scheduled reports are suppressed by default.
- An incomplete query cannot emit a misleading healthy heartbeat.
- Priority rules route captured events to the expected profiles.
- ManagedBy lookup has deterministic fallback behavior.
- Route failures do not erase successful receipts from other routes.
- Restarting a watcher/task preserves idempotency within the declared delivery guarantee.
- SMTP and at least one optional non-email adapter pass packed-artifact validation.

## 8. Branding and conditional presentation

**Related issues:** [#9](https://github.com/EvotecIT/EventViewerX/issues/9), [#73](https://github.com/EvotecIT/EventViewerX/issues/73), [#80](https://github.com/EvotecIT/EventViewerX/issues/80)

**Decision:** Defer. EventViewerX/Evotec branding is desirable, but customization and possible paid-product boundaries need a separate licensing/product decision. The MIT-licensed core will not introduce an unclear branding gate now.

### What this gives us

Organizations can apply a recognizable report identity and highlight high-value records without editing generated HTML or maintaining separate Excel/email formatting scripts.

### Proposed model

Extend the shared presentation projection with two validated inputs:

- `EventReportBranding`
- `EventConditionalFormatRule[]`

Branding options:

- product/report name;
- organization name;
- logo source and alt text;
- accent, background, and text color tokens;
- light/dark/automatic theme preference;
- optional footer and approved links.

Logo sources should support:

- embedded content supplied as a local file or byte resource;
- content-ID inline email attachment;
- HTTPS URL when the operator accepts remote loading;
- no logo.

The default email path should prefer an inline resource when a logo is provided, because external image loading is commonly blocked. Size and MIME type are validated before rendering.

Conditional rules use the same typed predicate model as event filtering:

```powershell
$format = New-EVXConditionalFormat `
    -Type ADGroupMembershipChange `
    -Where { $_.GroupName -in 'Domain Admins', 'Enterprise Admins' } `
    -Style Critical

Show-EVXEvent -Type ActiveDirectoryChanges `
    -ActiveDirectory CurrentForest `
    -Branding C:\EVX\branding.json `
    -ConditionalFormat $format
```

### Safety and portability

- Configurations select validated style tokens; they do not inject arbitrary CSS, HTML, Excel formulas, or scripts.
- Raw values are encoded exactly once at the renderer boundary.
- Unicode and non-ASCII values participate in predicates before HTML encoding.
- Color is not the only indicator; critical/warning styles include text/icon semantics and accessible contrast.
- Unsupported style features degrade explicitly per renderer.

### Renderer behavior

- HTML, email, and Excel consume the same matched style semantic.
- CSV remains data-only and may include an optional semantic severity/style column.
- Email uses conservative table/layout behavior and inline resources.
- Excel uses cell/table style APIs, never formulas constructed from untrusted event values.
- The default EventViewerX theme remains unchanged when no branding is provided.

### Acceptance gate

- The same typed rule marks the same rows in HTML, email, and Excel.
- Accented, CJK, right-to-left, and emoji values render and match correctly.
- Inline logos produce valid MIME/content-ID references and a useful text alternative.
- External images remain an explicit operator choice.
- Representative Outlook/webmail and browser rendering is inspected after the final implementation.
- Invalid colors, oversized images, unsafe links, and raw markup fail validation before rendering.

## 9. Provider-neutral central storage

**Related issues:** [#4](https://github.com/EvotecIT/EventViewerX/issues/4), [#40](https://github.com/EvotecIT/EventViewerX/issues/40), [#52](https://github.com/EvotecIT/EventViewerX/issues/52), [#58](https://github.com/EvotecIT/EventViewerX/issues/58), [#74](https://github.com/EvotecIT/EventViewerX/issues/74)

**Decision:** Defer. SQL Server is a plausible central-retention capability, but implementation waits for the product/licensing and deployment model. SQLite remains the supported store for the adopted work.

### What this gives us

Several collectors or scheduled tasks can retain and query one shared event history with consistent schema, checkpoints, duplicate groups, incidents, summaries, retention, and delivery outbox state.

SQLite remains the preferred zero-configuration local store. Central storage is an option, not a requirement for normal EventViewerX usage.

### Package and ownership model

Introduce `EventViewerX.Storage.Abstractions` containing provider-neutral contracts and models.

- The existing `EventViewerX.Storage` package remains the compatible SQLite implementation and adopts the abstraction.
- `EventViewerX.Storage.SqlServer` implements the same contract through DbaClientX SQL Server capabilities.
- Providers declare capabilities such as transactions, aggregation pushdown, advisory locking, bulk ingest, and migration support.
- EventViewerX does not reference `Microsoft.Data.SqlClient` directly.

This avoids making SQL Server consumers pull SQLite native assets or making the core event package depend on database providers.

### Core contract

`IEventStore` should own:

- schema initialization and version inspection;
- event writes with exact/transport identity;
- query and explain plan;
- checkpoint inspection and one transactional ingest operation that writes an
  event batch and compare-and-advances its consumer/query-profile plus stable
  target/channel checkpoint in the same commit. The checkpoint snapshot carries
  the source log generation/boundary identity used by the existing
  `EventCheckpointStore`, not only a record-ID high-water mark. A provider
  cannot expose successful checkpoint advancement before the corresponding
  rows are durable;
- incident and duplicate-group persistence;
- summary/aggregation execution where supported;
- retention/pruning;
- notification outbox and receipts when capability 7 is adopted;
- health/readiness probe.

Provider-specific configuration remains outside query models.

Concurrent ingestors supply the checkpoint version, source-generation/boundary
identity, and high-water mark they read. The store either commits the
deduplicated batch and new checkpoint atomically or reports a conflict without
partial advancement. A cleared/replaced log starts a reconciled new generation
according to the same strict/non-strict boundary policy as the existing
checkpoint engine; it never keeps filtering against an unreachable record ID
from the prior generation. The provider contract suite must inject failures
before and after each persistence step and prove that a restart can neither
skip a batch nor expose a checkpoint ahead of durable rows. It must also prove
query-profile isolation and clear/replacement generation recovery.

### Public experience

```powershell
$store = Open-EVXStore `
    -Provider SQLite `
    -Path C:\EVX\events.db

$central = Open-EVXStore `
    -Provider SqlServer `
    -ConnectionName EventViewerXProduction `
    -Credential (Get-Secret EventViewerXSql)

Show-EVXEvent -Store $central `
    -Type ActiveDirectoryChanges `
    -TimePeriod Last30Days
```

```text
evx store verify --profile central-store.json
evx report --store-profile central-store.json --type ActiveDirectoryChanges
```

Connection profiles contain provider, server/database, authentication mode, encryption requirements, and secret references. Plaintext passwords are rejected.

### Authentication options

The provider may support:

- Windows integrated authentication;
- username plus referenced secret;
- certificate or managed identity when the DbaClientX provider supports and validates it.

The EventViewerX surface exposes provider capabilities instead of pretending every authentication mode works everywhere.

### Schema and migration

- every schema has an explicit version and migration history;
- startup may inspect but does not silently perform destructive or long-running migration;
- `Update-EVXStoreSchema` supports `ShouldProcess`, migration plan output, backup guidance, and post-migration verification;
- concurrent writers use provider-owned transaction and locking behavior;
- raw event identity and JSON/schema versions remain forward-readable;
- retention operates in bounded batches with cancellation and progress.

### Query and aggregation

The shared query planner separates:

- provider-pushable predicates/aggregations;
- exact managed verification;
- bounded fallback;
- unsupported operations.

An `Explain` result must state whether a central query would require downloading an unbounded dataset. Such a query is rejected unless the caller supplies an explicit safe bound.

### Acceptance gate

- SQLite behavior and packed PowerShell artifact remain backward compatible.
- SQLite and SQL Server pass the same provider contract suite for writes, reads, checkpoints, exact identity, transport deduplication, retention, and summaries.
- Two different query profiles over one target/channel cannot advance one
  another, and log clear/replacement cannot strand ingestion behind the prior
  generation's record ID.
- SQL credentials and secrets never appear in logs, exceptions, JSON profiles, or reports.
- Concurrent ingestion is proven with transaction and retry behavior.
- Migration failure is recoverable and leaves a verifiable schema state.
- Vulnerability and transitive provider-package audits remain clean.
- Public artifacts use one coordinated EventViewerX version.

## Cross-capability implementation slices

The capabilities should be delivered in independently useful slices. Adoption of a later slice does not require committing to every item in the document.

### Slice A: requirements and target foundations

- Add the event requirements registry.
- Add the AD target discovery contracts and resolver.
- Add `Get-EVXTarget` and the opt-in `-ActiveDirectory` query/report surface. No target remains the local machine.
- Add discovery results to reports without merging them into query coverage.

**Useful outcome:** forest/domain reports no longer need hand-maintained DC arrays, and the selected scope is visible.

### Slice B: readiness and onboarding

- Compose existing probes into `EventReadinessEngine`.
- Add `Test-EVXReadiness`, `Get-EVXRequirement`, and `evx doctor`.
- Generate the requirements matrix.
- Publish the direct and WEC daily-report tutorials.

**Useful outcome:** operators can prove what is ready before scheduling a report.

### Slice C: event meaning

- Promote the enrichment pipeline and add consistent GPO resolution.
- Add the persistent historical context contract and SQLite context store for deleted/renamed objects.
- Add a SQLite store-first ingest operation that commits the deduplicated event
  batch and its generation-aware per-profile target/channel checkpoint in one
  transaction. The adopted scheduled workflow does not pipe a record-id
  checkpoint ahead of downstream storage.
- Add deterministic AD value normalizers.
- Add semantic occurrence grouping without deleting source observations.
- Leave general incidents and multi-event correlation deferred.

**Useful outcome:** reports explain logical changes instead of presenting disconnected raw rows.

### Slice D: analysis

- Add the shared aggregation engine and `Measure-EVXEvent`.
- Add SQLite pushdown and explain behavior.
- Render shared aggregate results in HTML and Excel.

**Useful outcome:** top lockouts, authentication sources, and trends become standard queries.

### Slice E: delivery and presentation

**Deferred pending TeamsX and branding/product decisions.** The design remains here for later evaluation but is not part of the current implementation goal.

- Add notification policies, durable IDs, receipts, and zero-result behavior.
- Keep SMTP in the existing CLI host and add one optional non-email adapter.
- Add validated branding and conditional style semantics.

**Useful outcome:** reports reach the right audience with less noise and consistent visual meaning.

### Slice F: central retention

**Deferred pending central-storage and product/licensing decisions.** SQLite remains the implementation target for capabilities 1-6.

- Extract storage abstractions without breaking SQLite.
- Add the DbaClientX-backed SQL Server provider.
- Add schema migration, provider health, contract tests, and central-store documentation.

**Useful outcome:** multiple collectors can share durable history and reporting.

## Recorded product decisions

1. The local machine is always the default. `CurrentDomain`, `CurrentForest`, named domain/forest, collectors, and trusts are opt-in.
2. Readiness uses every safe check available to the current identity, reports permission-limited evidence honestly, and gives actionable remediation without mutating the environment.
3. EventViewerX ships a complete compiled enrichment catalog. It does not expose custom scripts, process-local PowerShell providers, or executable definition logic.
4. Deterministic value normalization is adopted. General incidents and multi-event correlation are deferred.
5. Semantic duplicate handling uses non-destructive, versioned occurrence groups over immutable source observations.
6. Shared aggregation, trend, and chart contracts are adopted.
7. Notification policy and external transport work is deferred while TeamsX is redesigned. Mailozaurr remains available for user composition.
8. Branding customization is deferred until EventViewerX/Evotec branding and possible paid-product boundaries are understood under the MIT license.
9. SQL Server storage is deferred until central-retention and possible product/licensing boundaries are decided. SQLite remains the supported adopted store.

## Open implementation questions for adopted capabilities

1. Which remote mechanism, if any, should prove effective audit policy when the current identity has permission? The result must still distinguish configured, effective, observed, and unknown evidence.
2. Which event-derived facts belong in the first persistent context schema beyond GPO identity/history?
3. Which narrowly scoped compiled evidence-import contract should TestimoX use, if any, to share GPO history without coupling EventViewerX to TestimoX scanning?
4. Do any supported event/provider versions expose a causal discriminator that
   safely joins DC/PDC lockout observations? Until proven by real fixtures,
   ordinary 4740 observations remain separate or explicitly ambiguous.
5. Which normalized fields are stable enough for the first aggregation presets and charts?

## Gap audit beyond inherited PSWinReporting issues

The PSWinReporting tracker is only one input. Before the adopted scope is considered complete, EventViewerX needs a current event-security and operator-workflow audit.

The audit should compare existing typed definitions, Windows event requirements, public filters, watchers, checkpoints, store queries, and reports against these workflows:

- monitor one server, collector, domain, or forest for hours or days with durable checkpoint/store state;
- answer whether NTLMv1, NTLM fallback, Kerberos RC4/DES, weak certificate authentication, unsigned LDAP, or other legacy authentication appeared;
- detect audit-policy, event-log, firewall, service, scheduled-task, PowerShell, Defender, and local-security changes where Windows provides reliable events;
- track account, group, computer, GPO, OU, privilege, and rights-assignment changes with useful actor/target fields;
- explain what source/channel/audit setting is required when a selected signal is absent;
- distinguish “no matching event” from incomplete discovery, inaccessible sources, disabled auditing, truncation, and an empty healthy period;
- produce consistent raw-event, occurrence, aggregate, trend, HTML, Excel, CSV, and JSON views without separate report logic;
- identify useful signals that require directory/SYSVOL/state monitoring rather than event monitoring and leave those with TestimoX or another owning product.

Potential event types are candidates only after checking authoritative Windows semantics, supported operating systems, useful structured fields, expected volume, audit prerequisites, WEC behavior, and real EVTX evidence. The goal is not to add every Windows Event ID; it is to add signals that support a concrete investigation or monitoring decision.

### Current audit snapshot

The first source audit separates missing operator experiences from missing event parsers:

| Area | Current capability | Gap and decision |
| --- | --- | --- |
| NTLMv1 | `ADUserLogonNTLMv1` selects Security 4624 records whose `LmPackageName` is `NTLM V1`. | Keep the parser. Add a named authentication-health preset, requirements metadata, readiness evidence, aggregate by account/source/host, and a durable monitoring tutorial. |
| Kerberos RC4/DES | 4768, 4769, and 4770 projections expose ticket encryption evidence. Standard 4771 and 4772 payloads do not carry `TicketEncryptionType` and are failure evidence, not proof that encryption was strong or weak. | Limit weak-encryption filters and totals to events that expose an encryption type. Keep 4771/4772 in authentication-failure views with `EncryptionEvidence = Unavailable`; never count their null projection as non-weak. Microsoft identifies 4768 and 4769 as the primary RC4-audit events and warns that 4769 success auditing is very high volume on domain controllers. |
| LDAP signing | 2887 summary and 2889 detail definitions already exist. | Add requirements explaining diagnostic-level and volume differences, readiness classification, and a safe summary preset. Do not silently enable diagnostic logging. |
| Durable monitoring | `Get-EVXEvent` has identity-bound persisted checkpoints; `Show-EVXEvent -StorePath` supports overlap plus provenance deduplication. Advancing `-RecordIdFile` before downstream storage is not crash-safe, and the CLI watcher subscribes only to future events and recreates its JSONL file on start. | The adopted reliable multi-day workflow is SQLite store-first ingestion: read an overlap, deduplicate by provenance, and atomically commit rows plus the generation-aware per-profile target/channel checkpoint. Do not pipe a separately advancing record-id checkpoint into the store. A live watcher remains a low-latency supplement until it can use the same atomic resume contract across restart and disconnection. |
| Scheduled tasks | Security 4698 create and 4699 delete are typed. | Add 4700 enabled, 4701 disabled, and 4702 updated under one coherent task-lifecycle family. Preserve task XML and actor/process fields where the event version provides them. Microsoft places all five events under Audit Other Object Access Events. |
| Windows Firewall rules | Only Security 4947 modified is typed. | Add 4946 added and 4948 deleted to the same lifecycle family. State clearly that these Security events describe local rule changes and do not prove Group Policy rule creation. |
| Microsoft Defender Antivirus | No built-in Defender operational definition exists. | Add a focused first family for 1116 detection, 1117 action, and 5007 configuration change. Evaluate 1125/1126 network-protection audit/block as a related but separate higher-volume family. |
| Service installation | 7045 is inspected only by the narrow Npcap/NPF/network-monitor rule. | Evaluate a general service-install definition using Security 4697 and System 7045, preserving source semantics rather than treating the two observations as exact duplicates. Do not add it until real event-version fixtures and audit requirements are proven. |
| PowerShell logging | `Get-EVXPowerShellScript` and `PowerShellEventEngine` already own 4103/4104 fragment reassembly, scan/output/cache bounds, cancellation, and explicit incomplete-result tracking; no typed operational family integrates that owner with the general event/report catalog. | Defer a typed 4103/4104 family until data-volume, sensitive-content, WEC rendering, truncation, and redaction contracts are designed. Any future typed family must reuse `PowerShellEventEngine` rather than duplicate its reassembly or safety logic. Script-block content must not become a default broad collection target. |
| GPO identity after deletion | Event rules expose GPO identifiers, but live lookup after deletion can no longer recover a reliable current name. | Implement the historical context store and resolve by event time. EventViewerX may accept narrowly scoped, versioned evidence exported by TestimoX; it must not absorb TestimoX directory/SYSVOL estate scanning. |

Authoritative references for the first security-coverage slice:

- [Microsoft: Detect and remediate RC4 usage in Kerberos](https://learn.microsoft.com/en-us/windows-server/security/kerberos/detect-remediate-rc4-kerberos)
- [Microsoft: Audit Kerberos Service Ticket Operations](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/audit-kerberos-service-ticket-operations)
- [Microsoft: Audit Other Object Access Events](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/audit-other-object-access-events)
- [Microsoft: event 4702, scheduled task updated](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-4702)
- [Microsoft: event 4946, firewall rule added](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-4946)
- [Microsoft: Microsoft Defender Antivirus event IDs](https://learn.microsoft.com/en-us/defender-endpoint/troubleshoot-microsoft-defender-antivirus)
- [Microsoft: network protection events](https://learn.microsoft.com/en-us/defender-endpoint/attack-surface-reduction-windows-events)

### First implementation coverage

The first adopted implementation should therefore deliver:

1. requirements and readiness metadata for the security types already present;
2. durable scheduled monitoring examples for NTLMv1, Kerberos weak encryption, LDAP signing, and AD changes;
3. task lifecycle completion (4698-4702);
4. firewall rule lifecycle completion (4946-4948);
5. the focused Defender detection/action/configuration family;
6. shared occurrence grouping and aggregation over those families;
7. persistent event-time context beginning with GPO identity.

General service installation and PowerShell operational logging remain evidence-gated follow-ups. They are not rejected, but should not expand the first slice before its requirements, durability, grouping, and reporting contracts are proven.

These decisions focus the current work on deployability, trustworthy evidence, historical context, meaningful values, noise reduction, and analysis. They leave transports, branding, paid features, central SQL storage, and broad infrastructure scanning for deliberate later discussions.
