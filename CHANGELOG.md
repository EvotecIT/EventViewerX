# Changelog

## 4.1.0

- Make the modern .NET 8 and .NET 10 library targets trim and Native AOT compatible, including Detection, Evtx, Reporting and Storage. Enable strict analyzers and native consumer checks.
- Preserve typed rule members and use static built-in factories, interpreted accessors and source-generated JSON contracts. Add generated-contract entry points for caller-owned observation, evidence and stored values, while retaining annotated general reflection APIs.
- Exercise crash/stability reading, predicates, reporting, SQLite storage, evidence bundles, Sigma and EVTX templates in the native smoke for both modern targets.
- Include #159's Windows crash parsing fixes. Shutdown timestamps accept invisible direction marks, and WER bugchecks split the packed code and four parameters while preserving the dump path and report identifier.
- Include #160's patched Native AOT compiler baseline and locked restore coverage.
