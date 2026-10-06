# Reproducible investigations and bounded replay

EventViewerX keeps collection evidence, detection state, and derived results separate.
Use an investigation session to share a fixed body of evidence. Use a replay checkpoint
to continue an ordered detection job without evaluating its entire history again.
Both capabilities live in the core library and are available through PSEventViewer.

## Capture and reopen an investigation

An investigation directory contains `manifest.json`, `plan.json`,
`observations.jsonl`, `findings.jsonl`, and copied input files. The manifest retains
the time range, query identity and definition, parser versions, effective plan hash,
exact engine build, source completion receipts, execution limits, and SHA-256 hashes
and lengths for every artifact. Original filenames are retained separately from the
safe filenames used inside the directory.

```powershell
$inputFile = Join-Path $PWD 'System.evtx'
$events = @(Get-EVXEvent -Path $inputFile -Oldest -ErrorAction Stop)
$rules = @(Get-EVXDetectionPack | ForEach-Object { $_.GetRules() })
$plan = [EventViewerX.EventDetectionPlan]::Compile(
    [EventViewerX.IEventDetectionRule[]] $rules, $null)

$manifest = [EventViewerX.EventInvestigationManifest]::new()
$manifest.StartUtc = $events[0].TimeCreated.ToUniversalTime()
$manifest.EndUtc = $events[-1].TimeCreated.ToUniversalTime().AddTicks(1)
$manifest.QueryIdentity = 'System.evtx/all-records/oldest/v1'
$manifest.QueryDefinition = 'Saved System.evtx, all records, oldest first'
$manifest.ParserVersions.Add('Windows Eventing', [Environment]::OSVersion.Version.ToString())

$session = $events | Export-EVXInvestigation `
    -Path (Join-Path $PWD 'system-investigation') `
    -Manifest $manifest -Plan $plan -InputPath $inputFile

$replayed = Open-EVXInvestigation (Join-Path $PWD 'system-investigation') |
    Invoke-EVXInvestigation
$replayed.Findings
```

This example assumes a nonempty saved log. It deliberately leaves coverage
undeclared: readable events alone do not prove that every required source was
collected. Supply `Manifest.Sources` and `-Coverage` from verified collection results
when making completeness claims. Include correlation warm-up in the declared input
window. Source receipts identify the exact host/channel/query contract, not merely
an event ID or a display label.

Opening verifies all retained hashes. Replay verifies the bytes used for evaluation,
preserves event, receive, and processing timestamps, and leaves original outputs
untouched. It evaluates retained canonical observations; it does not run stored query
text, reconnect to the source, download a parser, or reparse the copied EVTX files.
Keep the original parser and engine packages if a separate parser comparison is needed.

Replay requires the exact engine build by default. `-AllowDifferentEngine` permits
an explicit comparison and marks coverage incomplete. A semantic version alone does
not establish that two locally built engines behave identically. Hashes detect changed
artifacts; the manifest is not a digital signature or a substitute for trusted custody.

Creation requires a new directory. The manifest is written last. A cancelled or failed
capture can leave an unfinished directory without a manifest; preserve or remove it
deliberately before retrying into a new directory. Default limits are 100,000 observations,
4 MiB per observation, and 100,000 findings, with additional correlation-state bounds.
Durable fields support scalar values, core event enums, IP addresses, and arrays.
Unsupported arbitrary objects fail explicitly instead of silently losing their type.

## Continue detection from a checkpoint

`Invoke-EVXDetection -Stream` processes each input immediately and retains only bounded
correlation state. Input must be ordered by UTC event time, then record ID, then ordinal
observation identity. The ordinary materialized mode remains available when input needs
sorting. `-Stream` cannot be combined with `-FromStore`, `-Trace`, or `-ReportKind`.

```powershell
# $orderedBatch contains only the next ordered input batch for this exact source.
$orderedBatch | Invoke-EVXDetection -Rule $rules -Stream `
    -SourceIdentity 'collector-a/tasks/all/v1' `
    -RetentionIdentity 'immutable-archive-generation-7' `
    -CheckpointOut (Join-Path $PWD 'replay-0001.json')

$nextOrderedBatch | Invoke-EVXDetection -Rule $rules -Stream `
    -SourceIdentity 'collector-a/tasks/all/v1' `
    -RetentionIdentity 'immutable-archive-generation-7' `
    -CheckpointIn (Join-Path $PWD 'replay-0001.json') `
    -CheckpointOut (Join-Path $PWD 'replay-0002.json')
```

Checkpoints preserve consumed and pending threshold, distinct-value, temporal,
ordered-temporal, and absence state. Restoration rejects changed rules or tuning,
engine builds, source identities, retention generations, execution-state bounds,
and corrupt state or cursors. A new file is written and flushed; existing checkpoints
are never overwritten. Preserve the last committed checkpoint if a process exits
while writing its successor.

Coverage uncertainty also survives restart. A successful later batch cannot erase an
earlier collection failure or missing source window. Recollect the affected history and
rebuild with a new generation to clear that uncertainty.

The .NET `EventDetectionReplaySession` exposes `LastEventTimeUtc`, `LastRecordId`, and
`LastObservationIdentity`. Use that cursor to request only the continuation from your
source. An exact repeated boundary observation is ignored; input before the cursor
invalidates the session. For an inclusive time query, apply the remaining record-ID
and identity tie-breakers before submission. `EventStore.ReadObservationsAsync` provides
canonical stored observations; select the continuation with `EventStoreQuery` and order
equal-time results before feeding the session. Existing `EvaluateDetectionAsync` and
`Invoke-EVXDetection -FromStore` retain their full-history semantics; checkpoint use is
explicit, not a silent change to historical query meaning.

Change the retention identity after pruning, replacing evidence, or correcting historical
records. A continuation query cannot discover a late record that its own lower boundary
excludes. The source owner must detect that change and invalidate the generation, then
rebuild from an earlier unaffected checkpoint. Bounded live reorder buffers are not
checkpointed; durable replay requires already ordered input.

The library does not atomically commit external alerts and checkpoints. Persist emitted
findings successfully before publishing a checkpoint, and make external delivery
idempotent using rule and evidence identity. For a durable worker, use the .NET session
with an acknowledged output sink; a PowerShell pipeline with nonterminating downstream
errors is not an acknowledged transaction.

## Confirm absence only with complete coverage

An `Absence` rule has two steps: a trigger and its expected completion. It groups by
a correlation field and sets a deadline using `Window`. A completion consumes the
oldest eligible trigger in its group. The completion interval is half-open:
`[trigger time, trigger time + Window)`. A completion exactly at the deadline is late.

```csharp
var rule = new EventDetectionRule(new EventDetectionRuleDefinition {
    RuleId = "tasks/missing-completion",
    Title = "Task did not complete within five minutes",
    Kind = EventDetectionRuleKind.Absence,
    GroupBy = "TaskInstanceId",
    Window = TimeSpan.FromMinutes(5),
    CoverageScope = "server01/tasks/all/v1",
    Steps = new[] {
        new EventDetectionStepDefinition { Name = "Started", EventIds = new[] { 100 } },
        new EventDetectionStepDefinition { Name = "Completed", EventIds = new[] { 102 } }
    }
});
```

Supply complete general `EventDetectionCoverage` and an `EventDetectionAbsenceWindow`
containing the as-of UTC time and `EventCoverageWindow` receipts for the exact scope.
Receipts must span the trigger-to-deadline interval without gaps. A failed overlapping
receipt, incomplete input, exceeded evaluation bound, or predicate failure prevents a
confirmed absence. The finding reports `Incomplete` with an explanation instead.
Do not mark a receipt complete because the last event is recent or a query returned zero rows.

The same pattern supports service-start/service-ready pairs. To detect stopped traffic,
use the expected heartbeat as both trigger and completion, grouped by collector/source.
That detects a missing next heartbeat after observed traffic; it does not infer the
existence of a collector that has never produced a trigger.

For a continuing worker, call `session.AdvanceWatermark(window)` only after processing
all source input before its as-of time. Persist its findings, then save the checkpoint.
Due decisions are consumed; later triggers remain pending. Evidence older than a finalized
watermark invalidates subsequent replay. In PowerShell, `-AbsenceWindow` with
`-CheckpointOut` applies this boundary before saving. Without an absence window, checkpoint
creation retains pending absence triggers. `Complete()` closes a session and reports any
remaining uncertainty; it cannot be checkpointed afterward.

## Watch collection and drain accepted actions

```powershell
$watcher = Start-EVXWatcher -LogName System -FilterXPath '*' `
    -ActionCapacity 256 -Action { param($event) $event.Id }

$watcher.Health
Stop-EVXWatcher -Id $watcher.Id
$deadline = [DateTime]::UtcNow.AddSeconds(30)
while (-not $watcher.DrainCompletion.IsCompleted -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 50
}
$watcher.Health
```

`ActionCapacity` bounds queued plus running PowerShell actions, independently of native
`BufferCapacity`. Admission closes on overload, requests collection stop, and retains
accepted actions for draining. Health exposes queued, running, completed, failed,
acknowledged, and rejected counts; oldest pending age; source lag; stopping state;
and a persistent overload indicator. `StopAfter` and `StopOnMatch` reserve delivery
before dispatch, including projected watcher groups. Group members share one health view.

Stopping collection is separate from finishing accepted actions. Retain the returned
watcher to inspect it after removal from the active registry. In .NET, await
`DrainCompletion` with your own deadline. In PowerShell, let the owning runspace process
its event actions while waiting; do not block that runspace with `Task.Wait()` or `.Result`.
A stuck user action cannot be force-drained safely. Acknowledgement means the action
returned or failed, not that an external system durably committed its side effects.

Source lag is receive time minus raw source time. It includes transport and processing
effects and is not a measurement of clock skew.

## Preview a rule-pack change

```powershell
Compare-EVXDetectionPack -Previous $oldPack -Current $newPack
$observations | Compare-EVXDetectionPack -Previous $oldPack -Current $newPack `
    -Historical -MaximumObservations 10000 -MaximumFindings 100000 -Options $options
```

The historical form evaluates both effective plans on the same ordered sample. It
returns appearing and disappearing findings, matched finding counts and their delta,
plan hashes, and new declared source selectors and selector combinations. A rule-version
change alone does not turn the same rule/evidence finding into a disappearance and appearance.
Source requirements are conservative declarations, not a proof that existing collection
already satisfies a changed rule. Input truncation, incomplete coverage, finding limits,
and evaluation failures make the preview incomplete. No rules are enabled and no alerts
are delivered by the comparison.

Declare coverage for the common historical sample, including both plans. A finite
event-ID, channel, provider, or typed scope must cover the selectors used by both plans;
otherwise the preview reports uncertainty even when collection succeeded for the old
plan. An omitted coverage dimension declares no filter in that dimension. Selector checks
are conservative and do not infer collection completeness from the events that happened
to occur. Broader collected scopes can cover newly introduced selectors without requiring
another collection run.

## Represent clock uncertainty

Pass independently established `EventClockEvidence` to the four-argument
`EventTimelineEngine.Create` overload. Each bound identifies a host, validity interval,
correction added to raw time, maximum uncertainty, and measurement provenance.
Timeline entries retain all three original clocks and expose a possible UTC interval.

`entry.CompareClockOrder(other)` returns `Before` or `After` only for disjoint supported
intervals. Missing evidence or overlapping intervals return `Ambiguous`. Conflicting
measurements widen the interval conservatively. Finding intervals include all their
evidence; one observation without a bound leaves the finding's ordering uncertain.
The existing raw-time display order remains deterministic and does not establish
cross-host causality. Receive delay is never converted into a clock correction.

Pass the timeline's `Entries` into the report engine or `Show-EVXEvent` to retain
`ClockBounded`, `ClockEarliestUtc`, `ClockLatestUtc`, and `ClockProvenance` in report
rows alongside the original three clocks. An entry without independent evidence has
`ClockBounded = false` and empty interval endpoints.
