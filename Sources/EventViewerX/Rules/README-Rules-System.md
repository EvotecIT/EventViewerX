# Event-type rule architecture

Event types turn raw Windows records into scenario-specific objects such as
failed logons, account lockouts, Group Policy changes, Kerberos failures, or
AAD Connect health signals.

The rule layer is a projection over the shared native engine:

1. An `EventTypeQuery` selects rules, machines, time, limits, culture, and
   enrichment.
2. `EventTypeEngine.ReadAsync` asks each rule for its source channel and event
   IDs.
3. Sources are grouped and partitioned into bounded
   `EventLogChannelQuery` instances.
4. `EventLogEngine.ReadBatchAsync` performs the native Windows queries.
5. Matching `EventObject` records are projected to `EventTypeRecord` rule
   results in source order.
6. Optional enrichment and checkpoint observation happen before a result is
   emitted.

The removed `SearchEvents` facade is not part of this flow.

## Rule contract

The normal rule inherits from `EventRuleBase` and owns its source metadata,
predicate, and projection:

```csharp
namespace EventViewerX.Rules.ActiveDirectory;

public sealed class ADComputerCreateChange : EventRuleBase {
    public override List<int> EventIds => new() { 4741, 4742 };
    public override string LogName => "Security";
    public override EventType Type => EventType.ADComputerCreateChange;

    public override bool CanHandle(EventObject eventObject) {
        return true;
    }

    public ADComputerCreateChange(EventObject eventObject)
        : base(eventObject) {

        Type = nameof(ADComputerCreateChange);
    }
}
```

Use `CanHandle` when several rules share an event ID or when an XML payload
field distinguishes the scenario:

```csharp
public override bool CanHandle(EventObject eventObject) {
    return eventObject.Data.TryGetValue(
               "ObjectClass",
               out string? objectClass) &&
           string.Equals(
               objectClass,
               "computer",
               StringComparison.OrdinalIgnoreCase);
}
```

The constructor maps the raw event into stable, scenario-specific properties.
Keep provider-specific parsing in the rule. Keep query, culture, remote
session, cancellation, export, and checkpoint logic in the shared engines.

## Adding a rule

1. Add the public scenario name to `EventType`.
2. Add one focused `EventRuleBase` implementation under the appropriate
   `Rules/<Area>` folder.
3. Declare the exact channel and positive event IDs.
4. Implement `CanHandle` for any provider-specific discriminator.
5. Map only useful, stable fields in the constructor.
6. Add a focused projection test with representative `EventObject` data.
7. Add live validation when the provider is available in the test lab.

No central event-ID table is required. Rule metadata stays with the
projection that understands it.

## Discovery modes

`EventTypeRecord` supports three discovery modes:

| Mode | Behavior | Use |
| --- | --- | --- |
| `Auto` | Combines explicitly registered factories with discovered rule types. | Default library and PowerShell use. |
| `Reflection` | Discovers concrete `EventRuleBase`/`IEventRule` types from EventViewerX. | Conventional runtime hosts. |
| `ExplicitOnly` | Uses only delegate factories registered before first query. | AOT, trimming, or tightly controlled hosts. |

Configure discovery once, before the first event-type query:

```csharp
// Roots all 89 built-in typed projectors without Assembly.GetTypes().
EventTypeCatalog.RegisterBuiltInRules();

// Optional application-owned projector.
EventTypeCatalog.RegisterRuleFactory(
    EventType.ADUserLockouts,
    "Security",
    new[] { 4740 },
    eventObject => new ADUserLockouts(eventObject),
    eventObject => eventObject.Id == 4740,
    typeof(ADUserLockouts));

EventTypeCatalog.Configure(EventRuleDiscoveryMode.ExplicitOnly);
```

Registration after initialization is rejected. This avoids partially changing
the rule catalog while queries are active. `Reflection` and the reflection half
of `Auto` are intentionally trimming-unsafe compatibility modes. A trimmed or
NativeAOT host must register built-in and application factories, then select
`ExplicitOnly`. CI publishes and executes `EventViewerX.AotSmoke` so the
checked-in registry cannot silently drift from the built-in catalog.

## Querying from C#

```csharp
var query = new EventTypeQuery(new[] {
    EventType.ADUserLogonFailed,
    EventType.ADUserLockouts
}) {
    MachineNames = new string?[] { "DC01", "DC02" },
    TimePeriod = TimePeriod.Last24Hours,
    ReadMode = EventReadMode.Full,
    MaxConcurrency = 4,
    MaxEvents = 500,
    ContinueOnRemoteFailure = true
};

var execution = new EventTypeQueryExecutionInfo();

await foreach (EventTypeRecord item in
               EventTypeEngine.ReadAsync(query, execution)) {
    Console.WriteLine(
        $"{item.TimeCreated:u} {item.TypeName} {item.MachineName}");
}
```

`MaxEvents` limits matching projected results. `MaxCandidates` separately
limits raw records evaluated by rules. This distinction prevents a selective
rule from silently returning too few matches.

`EventTypeQueryExecutionInfo` reports candidates examined, results emitted,
source failures, and limit state without changing the returned object stream.

## Querying from PowerShell

```powershell
Get-EVXEvent `
    -Type ADUserLogonFailed, ADUserLockouts `
    -MachineName DC01, DC02 `
    -TimePeriod Last24Hours `
    -MaxConcurrency 4 `
    -MaxEvents 500 |
    Select-Object TimeCreated, TypeName, MachineName, UserName, IpAddress
```

PowerShell is a thin adapter: it builds `EventTypeQuery`, supplies durable
checkpoint callbacks and optional DNS enrichment, and streams
`EventTypeEngine.ReadAsync`.

## Ordering, failures, and checkpoints

- Batch results are merged deterministically.
- Rule projection and optional enrichment complete before the checkpoint
  observer advances.
- A failed remote target can be isolated with
  `ContinueOnRemoteFailure`; local and programming failures remain terminal.
- Durable PowerShell checkpoints are scoped by machine and channel, guarded by
  a shared lock, and include generation metadata so a reset cannot be undone
  by an in-flight query.
- Native bookmarks are opt-in and remain part of the underlying event when
  requested.

## Enrichment

Enrichment is optional and ordered. Reverse DNS, for example, is bounded by a
whole-operation timeout and a concurrency limit. Failure or timeout annotates
the result rather than removing the event or advancing its checkpoint early.

Rules must remain useful without network enrichment. Provider payload data is
the source of truth; enrichment is additional context.

## Key files

- `EventTypeQuery.cs` — public scenario query contract.
- `EventTypeEngine.cs` and `EventTypeEngine.Projection.cs` — batching,
  ordered projection, and limits.
- `EventTypeCatalog.cs` — discovery, explicit registration, and rule creation.
- `EventTypeRecord.cs` — the stable typed-result envelope.
- `IEventRule.cs` — `IEventRule` and `EventRuleBase` contracts.
- `EventEnricher.cs` and `Enrichment/` — optional ordered enrichment.
- `Rules/` — provider/scenario projections.

## Design rules

- One source of truth for a scenario's channel, IDs, predicate, and mapping.
- No independent query engine inside a rule.
- No network call unless enrichment is explicitly enabled.
- No swallowed projection failures: errors are reported with the affected rule.
- No placeholder rules. Add a rule when its provider contract and useful
  projection are understood and testable.
- Prefer a focused rule over a large switch statement or central mapping table.
