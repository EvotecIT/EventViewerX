# EventViewerX

C# Windows event log library: querying live, remote and offline (`.evtx`) logs,
typed event definitions, detection packs and Sigma import, event storage,
investigations and reports. Ships with the `PSEventViewer` PowerShell module
(`*-EVX*` cmdlets) and the `evx` CLI.

## Layout

- `Sources/EventViewerX.sln`: all projects live under `Sources/`.
  - `EventViewerX/`: the core library (`Definitions/`, `Detection/`, `Catalog/`, ...).
  - `EventViewerX.Detection/`, `EventViewerX.Evtx/`, `EventViewerX.Storage/`,
    `EventViewerX.Reporting/`: add-on packages.
  - `PSEventViewer/`: binary cmdlets (`Cmdlet<Verb>EVX<Noun>.cs`).
  - `EventViewerX.Cli/`: the CLI. `EventViewerX.SigmaAudit/`: Sigma rule audit tool.
  - Tests: `EventViewerX.Tests/` (Windows), `EventViewerX.Portability.Tests/`
    (cross-platform), `EventViewerX.AotSmoke/`.
- Module root: `PSEventViewer.psd1`, `PSEventViewer.psm1`, `PSEventViewer.Tests.ps1`,
  Pester tests in `Tests/`, `en-US/` help, `Examples/`.
- `Build/`: PowerForge build and release scripts and their JSON configs
  (`module.json`, `release.json`, `release-validation*.json`).
- `Docs/`: cmdlet reference (generated) and guides. `Benchmarks/`: benchmark fixtures.

## Build and test

```powershell
dotnet build Sources/EventViewerX.sln -c Debug
dotnet test Sources/EventViewerX.sln -c Debug --no-build
dotnet test Sources/EventViewerX.Portability.Tests/EventViewerX.Portability.Tests.csproj
./PSEventViewer.Tests.ps1                             # Pester; loads Sources/PSEventViewer/bin/Debug
./Build/Build-All.ps1 -RunMode Plan                   # preview module, packages and CLI release
./Build/Build-Module.ps1 -RunMode Build               # module and NuGet packages
./Build/Build-Cli.ps1 -RunMode Build -Runtimes win-x64
./Build/Test-ReleaseConfiguration.ps1; ./Build/Test-ReleasePlans.ps1   # after changing Build/*.json
```

- Targets: libraries `net472;net8.0;net10.0`; cmdlets `net472;net8.0`; tests
  `net8.0-windows;net10.0-windows`; CLI and portability tests `net10.0`.
  SDK pinned in `global.json` (10.0.100).
- Missing XML docs (CS1591) are errors. CI also builds Release with
  `-warnaserror -p:NuGetAudit=true`.
- Release only: `./Build/Build-All.ps1 -RunMode Publish` signs and publishes the
  module, NuGet packages and CLI archives.

## Generated files

- `Docs/<Verb>-EVX<Noun>.md`, `Docs/README.md` and `en-US/PSEventViewer-help.xml`
  come from the cmdlet XML docs. Regenerate with
  `./Build/Build-Module.ps1 -RunMode Documentation`.
- Refresh `PSEventViewer.psd1` with `./Build/Build-Module.ps1 -RunMode Manifest`
  rather than editing it.

## Conventions

- C#: braces on the same line. Most files use file-scoped namespaces; match the
  file you're editing.
- Cmdlets use the `EVX` noun prefix and XML docs for help.
- PowerShell: support Windows PowerShell 5.1 (64-bit only) and PowerShell 7,
  K&R/OTBS braces, full parameter names, no `+=` array building.
- Investigations keep collection evidence, detection state and derived results
  separate (see `INVESTIGATIONS.md`).
- Commit and PR titles use Conventional Commits with Gitmoji:
  `fix(rules): 🐛 Correct audited Windows rules and report output`.

## Gotchas

- Live tests (`[LiveFact]`) need `EVENTVIEWERX_RUN_LIVE_TESTS=true` and lab
  access to remote machines.
- `Category=Soak` tests are long-running durability tests.
- The module refuses to load in 32-bit Windows PowerShell.
- Most of the library needs Windows event log APIs; only
  `EventViewerX.Portability.Tests` runs on Linux (CI uses Ubuntu).
