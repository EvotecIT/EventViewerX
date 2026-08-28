# EventViewerX roadmap

EventViewerX is the active home of PSEventViewer, PSWinReporting, and
PSWinReportingV2 development. The product should make Windows event data useful
without forcing users to choose between a low-level event reader, a reporting
tool, and a security-hunting tool.

This roadmap records current product boundaries, release blockers, and the
dependency-ordered work needed after 4.0. It is not a changelog. Completed work
belongs in release notes and pull requests rather than remaining as a permanent
list of checked tasks.

## Product direction

EventViewerX should provide one reusable engine for five related jobs:

1. Read live, remote, forwarded, and saved Windows events with explicit bounds
   and evidence about failures or incomplete coverage.
2. Project provider-specific payloads into stable, typed event records for C#,
   PowerShell, and the CLI.
3. Evaluate native and Sigma-compatible detections over individual events and
   bounded event sequences.
4. Produce explainable findings, timelines, aggregations, and operational or
   security reports.
5. Optionally retain normalized history when a workflow needs long lookback,
   restart-safe correlation, baselines, or auditability.

The intended pipeline is:

```text
Windows channel, WEC, or saved EVTX
    -> native query and bounded enumeration
    -> EventObject
    -> typed event projection
    -> canonical EventObservation
    -> native or Sigma-compiled detection plan
    -> EventDetectionFinding
    -> PowerShell, C#, CLI, reports, or optional storage
```

Reading and short-window analysis must work without storage. Reporting and
storage are consumers of core contracts; neither owns event classification or
detection logic.

## Product boundaries

- A query without an explicit target reads the local computer.
- Domain, forest, collector, and trusted-forest expansion are opt-in.
- Readiness assesses prerequisites with the permissions users already have. It
  does not silently change audit policy, channels, subscriptions, firewall
  rules, or scheduled tasks.
- Raw events remain evidence. Projection, normalization, enrichment,
  correlation, and aggregation add views without destroying observations.
- Partial failures, bounds, cancellation, dropped delivery, and incomplete
  coverage remain visible.
- Built-in behavior is compiled and typed. Portable definitions and detection
  packs contain data and declarative conditions, not arbitrary PowerShell or C#
  scripts.
- Event-driven enrichment may resolve identities already present in selected
  events. It must not turn a report into an unrequested AD or SYSVOL inventory.
- PowerShell 5.1, PowerShell 7, `evx.exe`, packed artifacts, and the .NET API
  describe the same contracts.
- New packages are created for dependency, platform, or deployment boundaries,
  not merely to organize namespaces.
- Alert escalation, ticket ownership, fleet policy, and user credentials remain
  host responsibilities unless a separately approved product owns them.

## Capability ownership

| Capability | Canonical owner | Boundary |
| --- | --- | --- |
| Native event queries, projections, observations, detections, correlation | `EventViewerX` | Reusable engine and public C# contracts |
| PowerShell commands and object streaming | `PSEventViewer` | Thin adapter over EventViewerX |
| CLI commands and process hosting | `evx.exe` | Thin adapter over EventViewerX |
| HTML, Excel, email, and presentation models | `EventViewerX.Reporting` | Optional presentation dependencies |
| Local durable history and historical query pushdown | `EventViewerX.Storage` | Optional DbaClientX-backed persistence |
| Sigma YAML/schema support | `EventViewerX.Detection` | Thin adapter package because YAML parsing adds YamlDotNet; execution remains in core |
| Saved-EVTX parsing on non-Windows systems | EventViewerX core or an optional adapter | Separate package only if a native/parser dependency requires it |
| Benchmark orchestration and result rendering | PowerForge | EVX declares workloads and validation |

`EventViewerX.Storage` and `EventViewerX.Reporting` should depend on neutral core
contracts, not on each other. PSEventViewer and `evx.exe` may compose both
packages for complete workflows without moving domain logic into either host.

## 4.0 release blockers

The repository already contains a broad 4.0 foundation, but the package should
not be published until the following current-head blockers are fixed and proven
from packed artifacts.

### Typed event correctness

- [x] Replace implicit first-match composite dispatch with explicit
  specificity/priority or a canonical `PrimaryType` plus `MatchedTypes`
  contract.
- [ ] Add overlap tests for every built-in composite and every shared
  `(channel, provider, event ID)` candidate set.
- [x] Ensure specialized 4624 NTLMv1 events are not hidden by generic logon
  projection.
- [x] Ensure specialized Group Policy, OU, and detailed directory-change events
  are not hidden by broader rules.
- [x] Correct Group Policy link option parsing: preserve raw flags, treat bit
  `0x1` as disabled, and bit `0x2` as enforced.
- [x] Correct the `qPLik`/`gPLink` discriminator typo and sweep sibling
  attribute-name comparisons.
- [ ] Prove source identity, event order, projected type, and projected fields
  with representative EVTX fixtures and focused synthetic payloads.

### Watcher and process reliability

- [x] Replace notification buffering that clears a batch before durable output
  succeeds with persist-before-acknowledge delivery.
- [x] Use a bounded channel with explicit backpressure behavior.
- [x] Give every batch an atomic identity and record pending, delivered, retry,
  and dead-letter state.
- [x] Propagate delivery-worker failure to watcher status and process exit.
- [x] Consolidate duplicated process execution into one cancellable bounded
  runner with process-tree containment and bounded post-kill cleanup.
- [x] Run burst, output-failure, cancellation, restart, and retry tests proving
  no silent loss or duplication.

### Package and documentation truth

- [x] Reconcile public cmdlet counts and capability lists with the compiled
  module exports.
- [x] Remove the `EventViewerX.Storage` dependency on
  `EventViewerX.Reporting`; move neutral rows, schemas, aggregation results, and
  completeness metadata to EventViewerX core where necessary.
- [x] Resolve the module architecture contract so optional SQLite/native assets
  do not unnecessarily restrict otherwise portable PowerShell use.
- [ ] Build, test, pack, install, import, and exercise the exact candidate
  artifacts on PowerShell 5.1 and PowerShell 7.
- [ ] Verify that EventViewerX, Reporting, Storage, the CLI, and PSEventViewer
  expose compatible versions and dependency graphs.
- [ ] Complete a delayed independent review and settle all validated in-scope
  release findings.

## Delivered foundation to preserve

The following capabilities already exist and must remain covered while the
engine changes:

- local-by-default and explicit multi-target discovery;
- typed requirements and readiness evidence;
- direct, remote, WEC, and saved-EVTX Windows query paths;
- bounded asynchronous enumeration and deterministic ordering;
- typed event projections and portable event definitions;
- deterministic normalization with raw provenance;
- non-destructive transport and semantic occurrence grouping;
- bounded aggregations, trends, and charts;
- optional DbaClientX-backed SQLite history and checkpointing;
- persistent event-derived Group Policy context;
- HTML, Excel, CSV, and email reporting;
- custom Windows event-provider authoring and validation;
- framework-dependent and self-contained CLI artifacts;
- representative security-monitoring presets and prerequisite evidence.

## Milestone A: compile the event execution plan

This milestone fixes classification correctness and removes repeated planning
work from the per-event hot path.

- [ ] Replace `EventRuleBase` objects that combine metadata, matching, and
  runtime data with immutable projector definitions plus data-only typed
  records.
- [x] Compile one immutable execution plan per query, watcher, or stored query.
- [x] Expand composites once, before event enumeration.
- [x] Index projector candidates by channel, provider, and event ID.
- [x] Precompute explicit specificity and deterministic tie-breaking.
- [x] Cache event-ID collections instead of allocating mutable lists from
  metadata property getters.
- [x] Remove per-event `ToList()` and repeated `Expand()` calls from core, CLI,
  and PowerShell watcher paths.
- [ ] Extract shared payload values once when several projections or detections
  need the same field.
- [ ] Retain lazy message, XML, binary attachment, and expensive enrichment
  materialization until the selected workflow requires them.
- [ ] Keep AOT/trimming-friendly explicit registration and validate reflection
  and explicit-only modes against the same contract suite.

Acceptance:

- the event fixture produces identical source identities and deterministic
  output ordering;
- every previously shadowed specialized type is represented according to the
  new overlap contract;
- an unrelated increase in the catalog does not make every event scan every
  projector;
- allocation per projected event, peak working set, and throughput are measured
  against the frozen pre-change artifact.

## Milestone B: establish the detection core

Detection rules answer whether an event or sequence is suspicious, important,
or operationally unhealthy. They do not replace typed event projectors.

- [x] Add `IEventDetectionRule`, immutable rule metadata, and a compiled
  `EventDetectionPlan` to EventViewerX core.
- [x] Add `EventObservation` as a canonical, report-neutral view over raw and
  typed events.
- [x] Add `EventDetectionFinding` with stable rule ID and version, title,
  severity, confidence, status, tags, entities, time range, evidence event
  identities, explanation, false-positive guidance, and completeness.
- [x] Support stateless selection, threshold/count, distinct-value, temporal,
  and ordered-temporal evaluation.
- [x] Index detection candidates by event type, log source, provider, channel,
  and event ID rather than scanning every enabled rule.
- [x] Compile field aliases, value conversions, wildcard matchers, and bounded
  regular expressions once.
- [x] Enforce regex timeouts, maximum candidate counts, maximum groups, maximum
  state bytes, and maximum correlation windows.
- [x] Emit an explicit incomplete/error finding or execution result when a
  safety bound prevents a valid conclusion.
- [x] Support environment tuning through typed filters, allowlists,
  suppressions, thresholds, and maintenance windows without editing the signed
  source rule.
- [x] Provide rule validation, explain-plan, dry-run, and fixture-test APIs.

Analysis must accept `IAsyncEnumerable<EventObject>` or observations and emit
findings as a stream. A caller may optionally write events and findings to
storage, but storage is not required for a stateless or bounded-window run.

## Milestone C: versioned native detection packs

Detection content should ship independently from engine implementation while
remaining reviewable and reproducible.

- [x] Define a pack manifest with pack ID, semantic version, minimum engine and
  observation-schema versions, authorship, license, content hash, rule list,
  and optional signature.
- [x] Make built-in and downloaded packs use the same validation and execution
  contracts.
- [x] Verify signatures and hashes before enabling externally sourced packs.
- [x] Preserve rule provenance in every finding and report.
- [x] Support enable/disable, severity override, tuning overlay, and pack update
  comparison without silently changing historical findings.
- [ ] Ship positive, negative, boundary, and known-false-positive fixtures with
  each built-in detection.
- [ ] Publish coverage by required channels, audit policies, roles, providers,
  and event types.

Initial native packs:

### Eventing integrity

- Security log cleared, channel disabled, channel full, or audit policy
  weakened;
- expected event stream stopped or developed a suspicious gap;
- WEC source lag, disconnect, duplicate delivery, or bookmark/checkpoint drift;
- provider/schema changes that prevent required fields from being projected;
- collection completeness and blind-spot evidence.

### Active Directory identity and privilege

- new account rapidly added to a privileged group;
- account enabled, password reset, then used;
- dormant or disabled account reactivated and logged on;
- privileged membership churn and unusual actor/target combinations;
- sensitive userAccountControl, SID history, SPN, delegation, or account-policy
  changes;
- directory replication rights and other high-impact permissions when the
  required audit evidence is available.

### Authentication modernization

- NTLMv1 use, privileged NTLMv1 use, and first-seen NTLMv1 by host or account;
- Kerberos weak-encryption and downgrade patterns;
- password spraying and distributed authentication failures;
- failure burst followed by success;
- privileged or explicit-credential logon from a new host;
- protocol, host, account, and source-network baselines with clearly labelled
  confidence and history requirements.

### Group Policy, AD CS, and governance

- GPO creation, modification, linking, enforcement, disabling, and deletion
  sequences;
- sensitive audit or security-setting changes delivered through policy;
- certificate template, enrollment, issuance, revocation, and CA configuration
  changes when authoritative events are available;
- changes performed by unexpected actors or outside approved windows.

### Execution, persistence, and endpoint protection

- scheduled-task and service lifecycle patterns;
- firewall-rule persistence or weakening;
- suspicious PowerShell operational events under explicit privacy and volume
  controls;
- Defender detection, remediation, exclusion, and protection-setting changes;
- driver, network-monitor, or other persistence-relevant provider events where
  EVX has a stable typed contract.

## Milestone D: Sigma interoperability

Sigma support should reuse the detection core rather than creating a second
query or correlation engine.

- [ ] Parse and validate the supported Sigma specification and JSON schema.
- [x] Map Sigma log sources and taxonomy fields to canonical EventObservation
  fields and typed EVX properties.
- [x] Compile Sigma selections, conditions, modifiers, filters, and correlations
  into `EventDetectionPlan`.
- [x] Support event-count, value-count, temporal, and ordered-temporal
  correlation where EVX can preserve the required semantics.
- [x] Report unsupported fields, modifiers, log sources, or correlation behavior
  before execution; never silently weaken a rule.
- [x] Preserve Sigma ID, status, level, tags, references, false positives,
  license, and source hash in findings.
- [x] Expose rule import, validation, explain, test, and execution through the
  .NET API, PowerShell, and CLI.
- [x] Compare equivalent native and Sigma-compiled rules for identical findings,
  evidence, and completeness.
- [x] Decide whether YAML/schema dependencies justify an
  `EventViewerX.Sigma` package. Keep the implementation in EventViewerX core if
  no meaningful dependency boundary exists.

## Milestone E: correlation, hunting, and timelines

- [x] Add bounded keyed correlation state with deterministic eviction.
- [x] Support actor, target, host, account, SID, IP address, process, activity,
  logon, and transaction pivots.
- [x] Build explainable cross-host timelines that retain every evidence event
  and distinguish event time, receive time, and processing time.
- [ ] Support live streaming windows and storage-backed historical windows
  through the same detection contract.
- [ ] Push historical candidate selection and supported aggregations into
  storage while retaining exact managed verification.
- [ ] Expose why a rule matched, which condition failed, and which evidence was
  unavailable.
- [ ] Track data-source coverage for each finding so a rule cannot claim a clean
  environment when required channels or hosts were missing.
- [x] Export findings and timelines as typed objects, JSON Lines, CSV, HTML,
  Excel, and compact email summaries without rerunning the query.

## Milestone F: reports that turn events into decisions

Reports should be built over observations, findings, and aggregations rather
than owning private query logic.

- [ ] **Collection coverage and cost:** hosts, channels, WEC sources, lag,
  gaps, volume, retained days, storage growth, and estimated processing cost.
- [ ] **Eventing integrity:** audit-policy changes, log clear/disable/full
  activity, provider/schema drift, watcher delivery health, and incomplete
  evidence.
- [ ] **Authentication posture:** NTLMv1, Kerberos encryption, LDAP signing,
  privileged logons, failures, spraying indicators, and modernization trend.
- [ ] **Identity lifecycle:** create, enable, disable, password reset, group
  membership, privilege, logon, and deletion timeline per account.
- [ ] **Privileged access:** privileged group and rights changes, actor/target
  pivots, subsequent logons, and first-seen host use.
- [ ] **Group Policy governance:** creation, edits, links, enforcement,
  inheritance-affecting changes, deletion, and event-time versus current names.
- [ ] **Certificate Services governance:** CA and template changes, enrollment
  activity, failures, revocation, and readiness gaps.
- [ ] **Execution and persistence:** scheduled tasks, services, firewall rules,
  endpoint protection, and related account/process evidence.
- [ ] **Detection health:** enabled packs, versions, signatures, required data
  sources, matches, suppressions, noise, incomplete runs, and stale tuning.
- [ ] **Unknown event and schema drift:** high-volume unmapped providers/event
  IDs, changed payload shapes, projection failures, and candidates for new typed
  definitions.
- [ ] **Incident timeline:** ordered findings and raw evidence with pivots,
  source coverage, annotations, and reproducible query metadata.

Every report must state its time range, targets, channels, query ownership,
limits, failures, completeness, pack versions, and whether storage-backed
history was used.

## Milestone G: storage and long-running collection

### Optional storage

- [x] Keep normalized rows, schemas, findings, checkpoints, and completeness
  contracts independent from presentation dependencies.
- [x] Add indexed finding and evidence storage without duplicating the detection
  engine.
- [ ] Define schema evolution, migration, backup, restore, retention, pruning,
  and corruption-recovery contracts.
- [ ] Batch writes transactionally and measure write throughput, database size,
  indexed query latency, and managed-verification candidate counts.
- [x] Keep direct/WEC duplicate provenance deterministic across repeated
  ingestion.
- [ ] Support historical baselines and restart-safe correlation with explicit
  required-history and warm-up status.
- [ ] Keep access control, encryption-at-rest policy, and backup ownership
  visible to the host/operator.

### Durable watcher and optional agent

- [x] Expose queue depth, oldest pending age, delivery lag, retry count,
  dead-letter count, checkpoint age, dropped events, and worker health.
- [x] Make checkpoint advancement conditional on the configured durability
  boundary.
- [x] Support local outbox delivery with acknowledgement, retry/backoff,
  idempotency, and bounded disk usage.
- [ ] Define shutdown, restart, upgrade, rollback, and damaged-outbox behavior.
- [ ] Prove continuous operation with long-running soak and burst tests.
- [x] Keep downstream transport integrations optional and credential ownership
  outside the core library.
- [ ] Build a managed Windows service/agent only after the library/CLI delivery
  contract is proven and explicit product intent approves deployment,
  configuration, health, and upgrade ownership.

## Milestone H: saved EVTX portability

EventViewerX already reads saved EVTX on Windows through Windows Eventing APIs.
The additional capability under consideration is reading saved EVTX files on
non-Windows systems.

- [x] Define one parser-neutral saved-event contract that preserves record ID,
  provider, channel, timestamps, payload XML/data, and corruption diagnostics.
- [x] Prototype cross-platform parsing behind the existing file-query surface
  in dependency-isolated `EventViewerX.Evtx`; expose it through C#,
  `Get-EVXEvent -PortableEvtx`, and `evx --portable-evtx`. Add a second,
  explicit caller-owned `evtx_dump` adapter for higher archive fidelity.
- [x] Disclose that provider-formatted messages may be unavailable without the
  originating provider resources and Windows message-rendering APIs.
- [x] Test clean, truncated, dirty, invalid-header-checksum, sparse/bad-chunk, and large
  EVTX files on Windows plus a clean fixture on Linux.
- [x] Exercise an archived fixture and record the current adapter failure
  (zero of 653 Windows-readable records); keep it outside automatic promotion.
- [ ] Add retained truncated and archived fixtures to CI and replace or improve
  the parser until both pass.
- [x] Compare event count, identity, order, timestamps, and payload fields
  against the Windows path.
- [x] Compare recoverability against two representative forensic parsers:
  `evtx_dump` 0.12.2 and `python-evtx` 0.8.1. Record equivalent clean,
  large, archive, sparse/bad-chunk, and truncated workloads in the fidelity
  gate notes rather than treating record enumeration as XML fidelity.
- [x] Measure throughput, allocation, and identity fidelity before promotion;
  the managed adapter currently fails the allocation/performance promotion
  gate and therefore remains explicit opt-in. The command adapter is faster
  and archive-capable but remains explicit because it executes a caller-owned
  binary, loses the final 100-nanosecond timestamp digit in JSONL, and does not
  recover the retained truncated sample.
- [x] Keep the capability in EventViewerX core when implemented natively without
  new dependencies. Split a package only when a parser/native dependency or
  platform asset creates a real distribution boundary.
- [x] Keep live channels, remote sessions, WEC administration, provider message
  rendering, and Windows configuration explicitly Windows-only.

## C#, PowerShell, and CLI experience

### C# API

- [x] Keep asynchronous streaming, cancellation, bounded concurrency, and
  execution metadata first-class.
- [ ] Provide builders and immutable options for query, detection, correlation,
  tuning, storage, and report workflows.
- [x] Avoid requiring Reporting or Storage references for core analysis.
- [ ] Provide stable schemas and serialization contracts for observations,
  findings, plans, packs, and completeness.
- [ ] Preserve trimming/AOT-friendly registration and document platform-specific
  APIs.
- [ ] Add XML documentation and focused examples for every public workflow,
  including failure and incomplete-result handling.

### PowerShell

- [ ] Keep cmdlets thin and stream objects without collecting entire runs unless
  the requested operation requires a complete snapshot.
- [x] Add workflow-oriented commands for pack discovery, rule validation,
  detection execution, finding inspection, and tuning; do not add one cmdlet per
  rule.
- [x] Support pipeline input from `Get-EVXEvent` into detection and reporting
  without re-reading the logs.
- [x] Expose explain plans, completeness, required audit settings, and
  unsupported Sigma behavior as structured objects.
- [x] Keep PowerShell 5.1 and 7 behavior aligned, including packed-module import,
  help, examples, and native asset selection.
- [ ] Measure module import, first query, steady-state streaming, serialization,
  and finding/report creation independently.

### CLI

- [x] Expose the same query, detection, correlation, pack, storage, and report
  engines without parallel implementations.
- [x] Preserve stable JSON/JSON Lines contracts, meaningful exit codes, and
  machine-readable summaries.
- [x] Add `--explain`, dry-run, validation, and health output suitable for Task
  Scheduler and service hosts.
- [ ] Keep framework-dependent and self-contained artifacts separately measured
  for size and cold start.

## Performance program

Performance work must use PowerForge benchmark suites with correctness
validation. Source scanning may identify candidates, but LINQ calls,
allocations, pooling, parallelism, or unsafe code are not optimization targets
by themselves.

### Required benchmark matrix

- [ ] Scales: 1,000, 10,000, 100,000, and 1,000,000 events where the fixture
  supports them.
- [x] Enabled detection counts: 1, 10, 100, and 1,000.
- [ ] Lanes: enumeration, typed projection, overlapping projection, stateless
  detection, native/Sigma equivalent detection, correlation, watcher delivery,
  storage write/query, report rendering, CLI cold start, and PowerShell import.
- [ ] Sources: saved EVTX, local channel, remote channel, and WEC where safe and
  reproducible.
- [ ] Metrics: elapsed time, events/second, managed allocation/event, peak
  working set, p50/p95/p99 time-to-finding, candidate rules/event, correlation
  state, queue depth, finding count, output size, and database size.
- [ ] Provenance: repository head/dirty state, package hashes, fixture hash,
  runtime versions, host details, enabled packs, plan hash, and skipped lanes.

### Correctness gates for every performance result

- [ ] Identical source event count, identity, and deterministic order for
  equivalent workloads.
- [ ] Identical typed projections for before/after comparisons.
- [ ] Identical finding IDs, evidence identities, severity, and completeness for
  equivalent native/Sigma or before/after comparisons.
- [ ] No silent drop, duplication, merge, or checkpoint advancement.
- [ ] Every successful lane proves the requested work occurred; failed
  validation fails the lane visibly.
- [ ] Unlike output schemas or materialization levels remain in separate tables
  and are not described as direct speed comparisons.

### Initial performance budgets

- [x] Zero per-event rebuilding of immutable query, projection, or detection
  plans.
- [x] Zero per-event cloning of selected type/rule collections.
- [x] Candidate evaluation grows with matching indexed candidates, not the total
  installed catalog.
- [ ] Long-running watcher and correlation memory remains bounded under declared
  limits.
- [x] Watcher burst validation reports zero loss and zero duplicate source
  identities.
- [ ] No throughput improvement is accepted when projection, finding, evidence,
  or completeness semantics change.
- [ ] Set numerical regression thresholds only after a reproducible baseline is
  recorded; thereafter require explicit review for regressions above the agreed
  budget.

## Cross-cutting quality work

- [ ] Split large implementation files by semantic responsibility before adding
  substantial behavior to them.
- [ ] Replace mutable public collections with immutable/read-only contracts where
  compatibility permits.
- [ ] Sweep cancellation, disposal, process, native handle, event subscription,
  and background-worker lifetime boundaries.
- [ ] Add contract tests for public serialization, package dependencies,
  PowerShell exports, CLI schemas, AOT registration, and generated help.
- [ ] Keep provider definitions, Sigma YAML, schemas, and templates in native,
  reviewable files rather than large generated strings.
- [ ] Add fuzz/property tests for payload parsing, normalization, GPO link flags,
  Sigma conditions, correlation bounds, and corrupted saved EVTX input.
- [ ] Document security assumptions for remote access, credentials, pack trust,
  regular expressions, report output, local databases, and provider installation.
- [ ] Record telemetry and diagnostics locally without collecting customer event
  content or identifiers by default.

## Delivery order

Work should proceed in this order because later features depend on earlier
correctness and performance contracts:

1. Fix the 4.0 typed-event and Group Policy blockers.
2. Make watcher delivery and process execution fail-safe.
3. Decouple Storage from Reporting and settle package/platform architecture.
4. Freeze fixtures and publish current-head performance baselines.
5. Implement the compiled event execution plan and verify semantic parity.
6. Add core observation, finding, detection-plan, and tuning contracts.
7. Ship the first native detection packs and decision-oriented reports.
8. Compile Sigma rules into the same detection engine.
9. Add bounded correlation, pivots, and cross-host timelines.
10. Extend storage for findings, baselines, and restart-safe correlation.
11. Prove the durable watcher contract before deciding on a managed agent.
12. Prototype cross-platform saved-EVTX parsing and promote it only after
    fidelity and performance gates pass.

## Definition of done

A roadmap item is complete only when all applicable boundaries are proven:

- the reusable EventViewerX owner implements the behavior once;
- PowerShell and CLI surfaces remain thin and expose the same semantics;
- focused tests protect correctness, bounds, failures, and compatibility;
- representative real EVTX or live lab evidence exists where synthetic data is
  insufficient;
- performance-sensitive work has equivalent-input PowerForge evidence;
- package dependency and architecture changes are validated from packed
  artifacts;
- public C#, PowerShell, and CLI documentation uses supported production paths;
- generated help/docs are refreshed from their source of truth;
- incomplete or platform-limited behavior is disclosed;
- reviewed source, packed artifacts, published packages, and installed runtime
  are reported as separate release states.

## Deliberately uncommitted product decisions

These ideas remain research or product decisions rather than implied promises:

- a centrally managed fleet agent or endpoint-response platform;
- hosted storage, hosted rule management, or Evotec-operated event collection;
- SQL Server or another central provider and its licensing/product boundary;
- incident assignment, escalation, and case-management ownership;
- arbitrary user code inside normalization, detection, or correlation;
- custom branding and paid licensing models;
- automatic changes to audit policy, WEC, firewall, channels, or endpoint
  configuration.

No placeholder cmdlets, configuration keys, packages, or compatibility shims
should reserve these decisions before their ownership and operating model are
approved.
