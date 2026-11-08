# Changelog

## 4.2.1

- Read BitLocker suspensions from event 773 in `Microsoft-Windows-BitLocker/BitLocker Management`, qualified by the BitLocker API provider. Event 24660 describes encryption on write, not suspension.
- Read Windows Update installation failures from the System log and qualify queries by the WindowsUpdateClient provider.
- Cover the corrected queries and provider payloads in regression tests and the strict .NET 8 and .NET 10 Native AOT smoke.

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
