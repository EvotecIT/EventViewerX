# Changelog

## 4.2.1

- Read BitLocker suspensions from Management event 773 and key protector changes from the BitLocker API provider, rather than unrelated encryption, privilege-use or DPAPI events. Preserve volume and protector identity.
- Read Windows Update failures from the System log, retaining the update title and error code for Store apps as well as KB updates.
- Qualify Windows, directory, LDAP, SMB, DFSR and IIS sources by their providers. Correct Hyper-V lifecycle channels and the virtual-machine turn-off event, and accept Application-log Group Policy preference providers.
- Parse classic DFSR insertion fields, namespaced task registration metadata, Defender configuration paths and native IIS binding fields. Keep certificate request receipt separate from issuance, preserve request text and hexadecimal serial numbers, and keep automatic log backup separate from clearing.
- Remove rule-routing properties from field discovery and reports. Use numeric severity for report states, display known-zone event times in UTC, preserve complete messages in record details and show error/warning counts in overview cards.
- Keep the existing DHCP lease, SQL database creation, promiscuous-mode, Hyper-V checkpoint creation and AD Connect run-profile identifiers, while marking their invalid native sources unavailable. Discovery exposes the reason; queries and composites fail clearly, and readiness reports the source contract failure before querying machines.
- Preserve detection-pack inspection and stored-history replay for unavailable source contracts, and reject unsupported native selections before checkpoint probes or predicate explanation.
- Update the endpoint-protection pack to 1.0.1: withdraw the invalid promiscuous-mode detection and describe packet-monitor driver service activity accurately. Preserve native log names and useful fallback actions in log-clearing/full reports.
- Extend redacted capture and native-schema regression coverage and the strict .NET 8 and .NET 10 Native AOT smoke.

## 4.2.0

- Add provider-qualified workstation health rules for application crashes and hangs, service failures, disk and NTFS errors, NVMe resets, WHEA errors, display-driver recovery, and boot/shutdown performance.
- Preserve unnamed insertion-string positions after empty XML data entries, so classic application hang details remain correctly aligned.
- Exercise captured health events, typed query projections and observation JSON in the strict .NET 8 and .NET 10 Native AOT smoke.
- Patch the private URI dependency graph and validate module-only releases without requiring standalone CLI archives.

## 4.1.0

- Make the modern .NET 8 and .NET 10 library targets trim and Native AOT compatible, including Detection, Evtx, Reporting and Storage. Enable strict analyzers and native consumer checks.
- Preserve typed rule members and use static built-in factories, interpreted accessors and source-generated JSON contracts. Add generated-contract entry points for caller-owned observation, evidence and stored values, while retaining annotated general reflection APIs.
- Exercise crash/stability reading, predicates, reporting, SQLite storage, evidence bundles, Sigma and EVTX templates in the native smoke for both modern targets.
- Include #159's Windows crash parsing fixes. Shutdown timestamps accept invisible direction marks, and WER bugchecks split the packed code and four parameters while preserving the dump path and report identifier.
- Include #160's patched Native AOT compiler baseline and locked restore coverage.
