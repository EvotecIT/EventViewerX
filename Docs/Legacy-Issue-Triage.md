# Legacy issue dispositions

This is a read-only assessment of the 19 open EventViewerX issues on
2026-10-04, against source commit `7793aa5` and the current investigation,
migration, reporting and operational guides. Most reports concern the frozen
PSWinReporting 1.x or PSWinReportingV2 2.x surfaces. No issue was closed,
commented on or relabelled during this assessment.

“Current path covered” means the successor has an implementation and relevant
source contracts or tests. It does not mean the original user's environment,
old schedule, attached screenshot or mail delivery was reproduced. Before
closing a conversation, establish the installed module, selected source,
permissions, retained input and current result.

| Issue | Assessment | Recommended disposition |
| --- | --- | --- |
| [#89](https://github.com/EvotecIT/EventViewerX/issues/89), DetectDC errors | Legacy discovery/report script. Current discovery is explicit, bounded and retains per-domain failures. | Point to `Get-EVXTarget` and readiness; request a current diagnostic if it persists. Do not label the screenshot reproduced. |
| [#87](https://github.com/EvotecIT/EventViewerX/issues/87), custom functions in definitions | GPO naming has an event-derived context store. Current portable definitions are declarative and do not execute arbitrary scripts. | Explain the supported naming path. Treat arbitrary user functions as a product/extension decision, rather than adding code execution to pack data. |
| [#86](https://github.com/EvotecIT/EventViewerX/issues/86), missing helpers and null computer | Legacy helper-module and command compatibility. The successor exposes compiled commands and explicit targets. | Migrate the whole schedule and qualify its installed ZIP; confirm the write target separately if null/explicit-target behavior still fails. |
| [#84](https://github.com/EvotecIT/EventViewerX/issues/84), getting started | Discoverability gap. | Use [five complete jobs](Investigation-Workflows.md), [Onboarding](Onboarding.md) and the migration guide. Validate one bounded job before scheduling. |
| [#83](https://github.com/EvotecIT/EventViewerX/issues/83), GPO display names | Current path covered by `GroupPolicyDirectoryAudit`, event-derived name context, rename and deletion handling. | Demonstrate retained context; unknown names remain GUIDs when no authoritative name event was observed. |
| [#80](https://github.com/EvotecIT/EventViewerX/issues/80), accented styling words | Legacy regex-based email word replacement. Current Unicode rendering is a separate path; there is no equivalent public word-replacement configuration. | Do not claim the old replacement bug fixed by a new renderer. Decide whether a typed formatting feature is needed before exposing one. |
| [#79](https://github.com/EvotecIT/EventViewerX/issues/79), V2 setup and email | Migration/discoverability. Current reporting separates an interactive HTML artifact from a compact email payload and transport. | Follow the migration and onboarding jobs; verify the scheduled identity and actual delivery separately. |
| [#73](https://github.com/EvotecIT/EventViewerX/issues/73), inline email logo | Legacy branding configuration has no matching public report option. HtmlForgeX.Email is the reusable presentation owner. | Keep this as an explicit branding API decision; do not add consumer HTML replacement or a second mail renderer. |
| [#71](https://github.com/EvotecIT/EventViewerX/issues/71), ForwardedEvents | Current collector query matches original source channels inside the collector container. Source and collector identities stay separate. | Use `-Collector` and WEC readiness rather than replacing source log names throughout a script. Obtain live subscription/coverage proof before declaring the user's fleet fixed. |
| [#69](https://github.com/EvotecIT/EventViewerX/issues/69), NTLMv1 tracking | Current path covered by the specialized NTLMv1 type and `AuthenticationHealth` selection, including composite-dispatch tests. | Demonstrate the typed/preset query with retained 4624 evidence and explain source prerequisites. |
| [#55](https://github.com/EvotecIT/EventViewerX/issues/55), opaque values/descriptions | Current normalization retains raw values, canonical values, normalizer identity and warnings. Unknown provider codes remain visible. | Request the actual unresolved provider value and fixture before adding a mapping. Avoid inventing a friendly meaning. |
| [#54](https://github.com/EvotecIT/EventViewerX/issues/54), missing SMTP host | Legacy configuration-to-transport path. Rendering a payload does not establish that a host or relay is configured correctly. | Migrate the delivery configuration, qualify transport under the intended identity, and retain failure/outbox evidence. No test mail was sent during this assessment. |
| [#52](https://github.com/EvotecIT/EventViewerX/issues/52), SQL credentials | Current EventStore uses local SQLite. Central SQL storage and its credentials are outside that contract. | Keep as a product/storage-provider decision; any reusable provider work belongs in DbaClientX. |
| [#50](https://github.com/EvotecIT/EventViewerX/issues/50), lockout observations on several DCs | Current occurrence grouping is an explicit view and retains observations. Different DC/record identities are not proof of a duplicate. | Validate a causal grouping policy against paired retained events. Preserve raw source identity and avoid destructive timestamp-based deduplication. |
| [#43](https://github.com/EvotecIT/EventViewerX/issues/43), most-common lockout chart | Current aggregation supports grouping, time buckets and top selections; report renderers consume the same result. | Use the lockout trend job and inspect aggregation/input completeness. |
| [#42](https://github.com/EvotecIT/EventViewerX/issues/42), include selected accounts/groups | Current typed predicates support exact include/exclude conditions through the shared query engine. | Use `New-EVXFilter` and the selected definition's actual field names; check native/managed filtering in the explanation. |
| [#38](https://github.com/EvotecIT/EventViewerX/issues/38), fields within fields | Structured provider XML and nested text embedded inside a scalar are different formats. The old ADSync example is insufficient to establish current end-to-end EVTX behavior. | Obtain a retained ADSync fixture and define the required scalar fields. Evaluate a bounded declarative extraction in the core; do not treat recursive arbitrary message parsing as already supported. |
| [#37](https://github.com/EvotecIT/EventViewerX/issues/37), combining subevents | Merging separate records requires correlation identity and order, rather than rescanning and overwriting evidence fields. | Use a bounded definition/correlation contract when the causal key is known. Retain a representative fixture before adding an ADSync-specific rule. |
| [#17](https://github.com/EvotecIT/EventViewerX/issues/17), table success/failure detail | Current reports expose source coverage, failure diagnostics, scan bounds and completion evidence; readiness distinguishes unavailable proof. | Demonstrate complete, empty, failed-source and capped examples. An empty table alone is not proof of successful exhaustive collection. |

The package checks validate installed module/tool behavior independently of
development imports. The history, streaming and evidence-export changes in the
active roadmap carry additional completion and occurrence guarantees; their
source candidates and published delivery state must be assessed separately.

Use [the roadmap](../ROADMAP.md) for central storage, branding and extension
decisions. Use a current retained fixture and focused regression proof for a
reported defect that remains reachable. Closing an old issue requires a
maintainer decision or a confirmed current resolution, not its age.
