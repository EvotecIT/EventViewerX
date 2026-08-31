# Security and ownership boundaries

EventViewerX processes administrative and security telemetry, but it is a
library and command-line tool rather than a security boundary. The host owns
the identity, operating-system permissions, data location, encryption,
retention, delivery credentials, and response workflow.

## Remote access and credentials

- Prefer the caller's Windows identity and least-privilege Event Log Readers
  access. Use explicit credentials only for named remote targets.
- EventViewerX does not persist a supplied `NetworkCredential`. The caller is
  responsible for creating it from an appropriate secret store and disposing
  the surrounding host state.
- Do not broaden channel SDDL merely to make a query succeed. Treat access
  denied as evidence, and configure the narrowly scoped service or operator
  identity outside the library.
- WEC, provider installation, channel administration, source creation, and log
  clearing can change machine state and normally require elevation. Keep those
  operations separate from read-only analysis identities.

## Detection packs, Sigma, and regular expressions

- Treat native packs and Sigma YAML as executable analysis configuration.
  Review their source, pin versions and content hashes, and require a trusted
  signature policy when rules come from outside the deployment owner.
- EventViewerX rejects unsupported Sigma behavior rather than weakening it.
  A successful import means the rule fits the documented EVX profile; it does
  not certify the detection's quality or suitability for an environment.
- Regex predicates can consume substantial CPU on adversarial input. Use
  reviewed patterns, bounded queries, rule candidate limits, and controlled
  pack sources. Do not accept arbitrary user-authored expressions in a
  privileged long-running process without an additional policy boundary.
- Tuning and suppression can hide findings. Store them as reviewed,
  version-controlled policy with owners and expiry dates where appropriate.

## Reports and exported evidence

HTML, JSON Lines, CSV, Excel, email, and raw XML can contain account names,
addresses, command lines, script content, and other sensitive evidence. Write
reports to an access-controlled location, protect temporary and email staging
directories, and apply the organization's retention and disclosure policy.
CSV export escapes formula-like values by default; disabling that protection
is a caller decision.

Rendered HTML is a local artifact, not an authenticated application. Do not
publish it to an untrusted web origin without the controls expected for that
origin. EventViewerX does not upload telemetry or customer event content by
default.

## Local storage, encryption, and backup

`EventViewerX.Storage` uses the shared DbaClientX SQLite owner for optional
history. The database is not required for live detection or short-window
reports. When storage is enabled:

- the host chooses the database path and must restrict its directory ACLs;
- EventViewerX does not claim transparent encryption at rest—use volume,
  filesystem, or a host-selected encrypted database policy;
- the operator owns retention, pruning, free-space monitoring, and secure
  deletion requirements;
- use the EventStore integrity, backup, restore, and retention APIs instead of
  copying a database while writes are active;
- test restores under the same package/runtime versions and keep backup
  credentials outside EventViewerX configuration;
- database rows, findings, checkpoints, and report files can have different
  retention obligations even when they describe the same source event.

## Durable outbox and checkpoints

The notification outbox is local durable state, not a mail or queue service.
It publishes each batch atomically, records acknowledgement and retry state,
fails closed on incompatible or damaged completed batches, and reports
incomplete staging bytes. The host owns downstream credentials and decides
when acknowledged/delivered or dead-letter data is removed.

Checkpoint advancement must occur only at the configured durability boundary.
Do not delete or edit checkpoint state to bypass an error; reset it through the
supported API so an older concurrent generation cannot restore stale progress.
During upgrades and rollbacks, retain an outbox reader compatible with every
pending manifest. A newer unknown schema intentionally blocks delivery and
checkpoint advancement.

## Saved EVTX and external adapters

Portable EVTX parsing handles untrusted binary input. Keep parsing bounded and
inspect corruption diagnostics. Provider-formatted messages may be unavailable
without the originating Windows resources.

`EvtxDumpSavedEventReader` runs the exact caller-owned executable path. EVX
does not download or update it. Pin and verify that binary through the host's
software-supply-chain process, and do not resolve an executable from an
untrusted working directory or `PATH` entry.

## Custom provider installation

Provider packages can install machine-wide manifests and message resources.
Validate hashes, signatures, signer policy, schema compatibility, provider
identity, and target machine before applying them. Preserve released package
bytes and message resources needed to render historical events. Removing those
resources is a data-lifecycle decision even when the provider itself is no
longer active.

## Incident response expectations

Findings carry evidence identity, coverage, pack provenance, and completeness;
they are decision support, not an automatic containment authority. A clean
report is meaningful only when required hosts, channels, event IDs, and time
ranges are declared and complete. Route response actions through a separately
authorized system with its own review, audit, rollback, and least-privilege
controls.
