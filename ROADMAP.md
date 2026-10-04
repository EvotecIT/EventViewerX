# EventViewerX roadmap

EventViewerX provides one engine for Windows event queries, typed projections,
detections, reports and optional retained history. This roadmap tracks remaining
work and product decisions. Release history and completed implementation details
belong in the repository's releases, pull requests and capability guides.

## Current capabilities and owners

| Capability | Owner | User path |
| --- | --- | --- |
| Local, remote, WEC and saved-EVTX queries; typed definitions and normalization | EventViewerX core | C#, `Get-EVXEvent`, `evx query` |
| Native packs, Sigma compilation, bounded correlation and finding provenance | Core and EventViewerX.Detection | `Invoke-EVXDetection`, CLI detection, shared plans |
| Aggregation, occurrence grouping, timelines and Group Policy context | Core contracts | `Measure-EVXEvent`, report pipelines and context stores |
| Durable SQLite history, findings, checkpoints and retention | EventViewerX.Storage over DbaClientX | `Show-EVXEvent -StorePath`, store query and integrity commands |
| HTML, Excel, CSV and compact email | EventViewerX.Reporting over HtmlForgeX and OfficeIMO | `Show-EVXEvent`, reusable snapshots and renderers |
| PowerShell and CLI hosting | Thin PSEventViewer and EventViewerX.Cli adapters | Consistent bounded queries and operational evidence |
| Build, packages, release qualification and performance orchestration | PowerForge/PSPublishModule | Build configurations, artifact checks and benchmark gates |
| Portable saved-EVTX parsing | EventViewerX.Evtx | Explicit portable file mode; live Windows channels retain native APIs |

Use [the investigation workflows](Docs/Investigation-Workflows.md) for complete
jobs and [the migration guide](Docs/Migration-4.0.md) when replacing a legacy
schedule. Domain, forest, trusted-forest and collector expansion are explicit.
Readiness checks do not configure Windows. Original events remain evidence;
grouping and normalization add views without silently deleting observations.

## Active improvement backlog

The 2026-10-04 assessment produced the following reviewable changes. Open PRs
represent source candidates; they do not establish a published package or an
installed runtime. Merge and release gates apply separately.

- [ ] Settle historical replay, occurrence multiplicity and completion evidence
  ([#131](https://github.com/EvotecIT/EventViewerX/pull/131)).
- [ ] Settle source-query streaming and read-mode parity
  ([#132](https://github.com/EvotecIT/EventViewerX/pull/132), depends on #131).
- [ ] Settle checkpoint lookup allocation
  ([#134](https://github.com/EvotecIT/EventViewerX/pull/134)).
- [ ] Settle archive resource handling, cancellation and atomic publication
  ([#135](https://github.com/EvotecIT/EventViewerX/pull/135)).
- [ ] Settle distinct-value correlation state and its comparable performance
  proof ([#136](https://github.com/EvotecIT/EventViewerX/pull/136)).
- [ ] Settle portable EVTX fidelity and allocation
  ([#137](https://github.com/EvotecIT/EventViewerX/pull/137)).
- [ ] Settle export privacy policies and verifiable evidence bundles
  ([#138](https://github.com/EvotecIT/EventViewerX/pull/138), depends on #132).
- [ ] Settle packed CLI and PowerShell ZIP qualification in ordinary CI
  ([#139](https://github.com/EvotecIT/EventViewerX/pull/139)).
- [ ] Qualify compact email presentation work and full-report column analysis
  against comparable workloads and rendered output.
- [ ] Qualify standalone CLI archives on their declared platform and architecture
  matrix before publication. Normal tool-install proof covers Windows/Linux;
  it does not replace ARM64 or macOS runtime qualification.
- [ ] Publish approved artifacts only after signing, package identity,
  installation and runtime checks pass and publication is authorized.
- [ ] Reconcile [legacy issue dispositions](Docs/Legacy-Issue-Triage.md) with
  current reports before closing or transferring conversations.

Remove settled PR items after their behavior is integrated. Keep current user
paths in guides and contracts, rather than retaining completed milestone lists.

## Quality and performance gates

A successful empty result is useful only when its selected input was complete.
Bounds, failed sources, unavailable evidence, cancellation and delivery state
must remain visible through queries, detections, aggregation, reports and
retained history. Readiness `Unknown` remains distinct from `Pass` and `Fail`.

Use focused contract tests for real regressions and direct fixture, package and
rendered-output proof for the paths users run. Windows PowerShell 5.1 and 7,
.NET Framework 4.7.2, .NET Standard 2.1, .NET 8 and .NET 10 retain their declared
boundaries. Portable EVTX reads do not imply portable live-channel access.

Measure before changing hot paths. Compare the same fixture, counts,
identities, ordering, field values, completion evidence and runtime settings.
Keep allocation and scale evidence separate from noisy workstation timing.
[Performance gates](Docs/Performance-Gates.md) describe the controlled suites;
timing-sensitive gates do not belong in an uncontrolled CI smoke test.

Interactive full-detail reports retain their selected rows. Use explicit
collection limits, aggregation and bounded snapshots for large investigations;
a compact email should format only its allocated digest rows. Streaming queries
serve workflows that do not need a complete in-memory report.

## Product decisions requiring an operating model

These remain decisions rather than reserved commands or implied promises:

- A fleet agent, central collection service or endpoint-response platform.
- Hosted storage, rule management, incident assignment or escalation.
- Central SQL Server or other storage providers, routed through DbaClientX,
  with explicit tenancy, credentials, retention and migration contracts.
- Arbitrary user code in normalization, detection or correlation. Current
  definitions and packs are declarative; trusted compiled extensions belong in
  the owning core contract.
- Public branding and email-logo configuration, licensing or paid editions.
- Automatic audit-policy, WEC, firewall, channel or endpoint changes.

Prefer reusable ownership and a concrete consumer need before adding a package,
public API, dependency, service or scheduler. Do not reserve placeholder surface
for a decision that has no approved owner or deployment contract.
