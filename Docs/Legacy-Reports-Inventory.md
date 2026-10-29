# Legacy report API inventory

`EventViewerX.Reports.*` lives in the core `EventViewerX` assembly. It predates
the current `EventViewerX.Reporting` analysis and presentation contracts. W1
keeps the published API while I map callers and replacement behavior for the
reporting redesign.

| Published entry point | Current repository callers | Replacement work |
| --- | --- | --- |
| `EventViewerX.Reports.Live.LiveEventReportEngine` | Legacy family and unit tests | Map live query results, partial-source failures, and coverage to the current report snapshot. |
| `EventViewerX.Reports.Inventory.EventCatalogReportEngine` | Legacy family and unit tests | Preserve provider/channel inventory and failures through a typed investigation view. |
| `EventViewerX.Reports.Stats.EvtxStatisticsEngine` | Legacy family and unit tests | Compare counts and grouping with `Measure-EVXEvent` and the current aggregation engine. |
| `EventViewerX.Reports.Security.SecurityEvtxSummaryEngine` | Legacy family and unit tests | Map security summaries to typed observations and provenance. |
| `EventViewerX.Reports.Correlation.EventTypeCorrelationEngine` | Legacy family and unit tests | Retain timeline and correlation evidence in the new analysis contract. |

The `*QueryExecutor` implementations under this namespace are internal. Tests
call some of them through `InternalsVisibleTo`; they are not a separate public
API promise. A source search found no direct production callers of the five
facades elsewhere in this repository. That search cannot establish whether
published NuGet consumers use them.

Before a major-version removal, compare the public methods and result fields
with the new analysis contracts, run a consumer inventory, publish migration
examples, and validate real report output. Keep the legacy entry points in W1;
their migration belongs with the W2 library work or W3 reporting redesign.
