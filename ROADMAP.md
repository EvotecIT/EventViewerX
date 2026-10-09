# EventViewerX roadmap

EventViewerX provides one engine for Windows event queries, typed projections,
detections, reports and optional retained history. This roadmap tracks remaining
work and product decisions. Release history and completed implementation details
belong in the repository's releases, pull requests and capability guides.

## Current capabilities and owners

| Capability | Owner | User path |
| --- | --- | --- |
| Local, remote, WEC and saved-EVTX queries; streaming, typed definitions and normalization | EventViewerX core | C#, `Get-EVXEvent`, `evx query` |
| Native packs, Sigma compilation, bounded correlation and finding provenance | Core and EventViewerX.Detection | `Invoke-EVXDetection`, CLI detection, shared plans |
| Aggregation, occurrence grouping, timelines and Group Policy context | Core contracts | `Measure-EVXEvent`, report pipelines and context stores |
| Durable SQLite history, findings, checkpoints and retention | EventViewerX.Storage over DbaClientX | `Show-EVXEvent -StorePath`, store query and integrity commands |
| HTML, Excel, CSV and compact email | EventViewerX.Reporting over HtmlForgeX and OfficeIMO | `Show-EVXEvent`, reusable snapshots and renderers |
| Export privacy profiles and verifiable evidence bundles | EventViewerX core reporting contracts | Detached exports, pseudonyms and bundle manifests |
| PowerShell and CLI hosting | Thin PSEventViewer and EventViewerX.Cli adapters | Consistent bounded queries and operational evidence |
| Build, packages, release qualification and performance orchestration | PowerForge/PSPublishModule | Build configurations, artifact checks and benchmark gates |
| Portable saved-EVTX parsing | EventViewerX.Evtx | Explicit portable file mode; live Windows channels retain native APIs |
| Captured supplemental logs and device facts, evidence-backed findings and offline replay | EventViewerX core investigation contracts | `Get-EVXDiagnostic`, endpoint investigations and captured-evidence reports |

Use [the investigation workflows](Docs/Investigation-Workflows.md) for complete
jobs and [the migration guide](Docs/Migration-4.0.md) when replacing a legacy
schedule. Domain, forest, trusted-forest and collector expansion are explicit.
Readiness checks do not configure Windows. Original events remain evidence;
grouping and normalization add views without silently deleting observations.

## Active improvement backlog

Current source includes historical replay and occurrence guarantees, visible
completion evidence, streaming and read modes, indexed checkpoints, bounded
distinct correlation, portable EVTX allocation improvements, export privacy and
evidence bundles, packed-artifact CI, and responsive compact email. The remaining
work concerns release delivery, further full-report measurement and legacy
conversations.

- [ ] Profile full-detail HTML reports on representative large investigations.
  Preserve snapshot behavior and measure rendering allocation separately from
  bounded email digests.
- [ ] Publish approved artifacts only after signing, package identity,
  installation and runtime checks pass and publication is authorized.
- [ ] Reconcile [legacy issue dispositions](Docs/Legacy-Issue-Triage.md) with
  current reports before closing or transferring conversations.

## Captured-evidence investigation scope

Supplemental text logs and captured device facts belong with an event investigation
when they explain the behavior behind an observed Windows failure. Findings retain
their source coordinates, uncertainty and replay inputs. Contextual error guidance,
privacy controls and navigation improve this workflow without taking ownership of
endpoint management.

- [ ] Qualify further parser or diagnostic-pack proposals against representative
  captures: identify a concrete investigation question, preserve the original bytes,
  document missing evidence and prove reproducible findings before adding support.
- [ ] Consider small offline Intune or Autopilot packs only when those captures
  demonstrate useful correlations between Windows events and supplemental logs.
- [ ] Assess a local collection profile only for a specific investigation, with
  explicit opt-in behavior and bounded collection. Acquisition is a separate
  contract from interpretation of supplied evidence.

Repository fixtures and documented examples qualify the implemented offline
contracts. They do not establish coverage of every deployed agent version or
enrollment lifecycle. A larger feature list does not determine the next parser or
pack; a demonstrated evidence gap does.

Graph enrichment belongs in GraphEssentialsX, and AI adapters belong in
IntelligenceX. Their integration requires a concrete consumer contract. A new
desktop application, deployment agent, remote command runner or fleet service
requires its own product and operating model.

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

Qualify each standalone CLI archive on its declared operating system and
architecture before publication. A successful build or a tool installation on
another architecture does not establish native archive runtime behavior.
Keep archive hashes and source identity with the query, ingestion, reload and
integrity evidence for the release candidate.

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
