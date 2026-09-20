# Windows Event Collector fleet operations

This document describes the current 4.0 source tree. Version 4.0 is not yet
published or released.

EventViewerX treats Windows Event Collector as an evidence transport, not as a
single healthy/unhealthy switch. Fleet readiness needs five separate proofs:
desired subscription configuration, source authorization, runtime enrollment,
heartbeat freshness, and actual event delivery.

## 1. Plan the fleet from event requirements

Start with a built-in type or scenario so the same source contract drives the
query, WEC XML, readiness checks, reports, and detections.

```powershell
Get-EVXRequirement -Type ActiveDirectoryAuthentication

$definition = New-EVXCollectorSubscription `
    -Name EventViewerX-Authentication `
    -Type ActiveDirectoryAuthentication `
    -SubscriptionType SourceInitiated `
    -CollectorHostName WEC01.contoso.com `
    -AllowedSourceSid $domainControllersSid `
    -DeliveryMode Push

$definition | Set-EVXCollectorSubscription -InitializeCollector -Confirm:$false
```

Source-initiated fleets should use an explicit source SID/SDDL policy and
Active Directory discovery only when the operator intentionally selects that
scope. Collector-initiated subscriptions should list exact sources.

## 2. Detect configuration drift

`Get-EVXCollectorSubscription` returns normalized XML, query definitions,
enabled state, destination log, and source authorization. Store the generated
definition in source control and compare normalized snapshots during change
review. Applying a typed definition verifies the persisted XML. A failed apply
restores the prior definition; if both apply and rollback fail, EventViewerX
reports that persisted state is unknown instead of claiming success.

Remote inventory is read-only. WEC mutation and native runtime status are
local-only Windows contracts. Use an explicitly authorized remote PowerShell
session to run the same command on a collector; EventViewerX does not pretend
that Event Log RPC can mutate or fully diagnose a remote WEC service.

## 3. Assess enrollment, coverage, and heartbeat lag

```powershell
Test-EVXReadiness `
    -Scenario AuthenticationMonitoring `
    -Collector . `
    -SubscriptionName EventViewerX-Authentication `
    -ActiveDirectory CurrentForest `
    -MaximumHeartbeatAgeMinutes 15
```

The readiness report checks Wecsvc, WinRM, a listener, ForwardedEvents,
subscription enablement, typed query coverage, expected-source enrollment,
runtime errors, and the optional operator-owned heartbeat-age policy. Omitting
`MaximumHeartbeatAgeMinutes` leaves heartbeat timestamps as evidence and does
not invent an organizational threshold. A missing timestamp is `Unknown`, not
fabricated success or failure.

For direct inspection:

```powershell
Get-EVXCollectorSubscription -Readiness
Get-EVXCollectorSubscription -Name EventViewerX-Authentication `
    -IncludeRuntimeStatus -IncludeSourceAuthorization
```

## 4. Prove completeness end to end

Active runtime state and a fresh heartbeat prove connectivity, not delivery
completeness. Use a disposable correlation marker:

1. Record the source channel and record ID before the test.
2. Emit a uniquely identifiable event on each selected source through an
   approved test provider or an existing safe test event.
3. Read the exact source record IDs directly.
4. Wait within the configured delivery/heartbeat budget.
5. Read ForwardedEvents through EventViewerX and require the same source
   computer, source channel, event ID, record ID, and correlation marker.
6. Remove only the disposable subscription and test artifacts.

Do not infer completeness from `EventsProcessed`, `Active`, or a successful
subscription create/delete cycle. Those are useful health signals but do not
prove that the intended record arrived without loss or duplication.

## 5. Operate and roll back safely

- Generate subscription XML before mutation and review the exact source ACL,
  query, destination, delivery mode, heartbeat, latency, and retention needs.
- Use `Set-EVXCollectorSubscription` for verified local apply. It retains the
  previous XML, verifies the new state, and attempts bounded rollback on error.
- Keep ForwardedEvents retention large enough for the worst expected outage and
  ingestion lag. EventViewerX does not guess a fleet-wide retention policy.
- Persist downstream checkpoints only after the corresponding report, store,
  or outbox artifact is durable.
- Treat `Unknown` readiness as missing evidence that needs investigation, not
  as a pass.
