# Custom Windows Event providers

EventViewerX makes a real manifest-based Windows Event provider a build-once,
deploy-many artifact without requiring a Windows development toolchain.

You define named and typed fields in a PowerShell hashtable, JSON, or C# model.
Any Windows machine running EventViewerX or PSEventViewer can compile one
`.evxprovider` package in-process. The build and target machines do not need
the Windows SDK, Visual Studio, MSVC, a C# compiler, generated source, or a
package repository.

## The workflow

1. Define provider identity, channels, events, messages, and payload fields.
2. Validate the definition.
3. Build and optionally sign an `.evxprovider` wherever the module or library
   is available.
4. Distribute the single package through normal software deployment.
5. Install it elevated on each target.
6. Write events by event name and field name.
7. Build upgrades against the released package as a compatibility baseline.
8. Retain old resources so historical EVTX messages remain renderable.

EventViewerX writes the Windows message table, event metadata, and resource-only
PE directly in managed code. Runtime event writes use the registered manifest
and native Windows Eventing API.

## PowerShell: hashtable to working provider

The concise form creates a conventional `<ProviderName>/Operational` channel.
A single event or channel may be one hashtable; use arrays only for several
items.

```powershell
$provider = @{
    ProviderName = 'Contoso.Scanner'
    ProviderGuid = '7a87f315-4b5e-40a2-b748-b0cdd8adab41'
    Version      = '1.0.0'
    Events       = @{
        Name    = 'ScanCompleted'
        Id      = 1000
        Message = 'Scan of {ComputerName} found {FindingCount} issues.'
        Fields  = [ordered] @{
            ComputerName = 'String'
            FindingCount = 'UInt32'
        }
    }
}

$validation = $provider | Test-EVXProviderDefinition
if (-not $validation.IsValid) {
    $validation.Errors
    throw 'Provider definition is invalid.'
}

New-EVXProviderPackage `
    -Definition $provider `
    -OutputPath .\Contoso.Scanner-1.0.0.evxprovider `
    -Force
```

The runnable version is
[`Build-CustomProvider.ps1`](../Examples/Build-CustomProvider.ps1). Its
[`CustomProvider.definition.json`](../Examples/CustomProvider.definition.json)
is a source-controlled JSON equivalent.

## Build requirements

The builder is part of EventViewerX. It does not launch `mc.exe`, `rc.exe`,
`link.exe`, Roslyn, or another compiler, and it does not generate or execute
source code. The same command works on a clean Windows host with only the
module or library and its normal runtime dependencies:

```powershell
New-EVXProviderPackage `
    -DefinitionPath .\provider.definition.json `
    -OutputPath .\Contoso.Scanner-1.0.0.evxprovider `
    -Force
```

The completed package records the managed compiler identity and version and
hashes every payload file. Building does not register the provider or require
elevation; installation and removal are the machine-wide operations that must
run elevated.

## Package signing

Sign with an RSA code-signing certificate containing a private key:

```powershell
New-EVXProviderPackage `
    -DefinitionPath .\provider.definition.json `
    -OutputPath .\Contoso.Scanner-1.0.0.evxprovider `
    -CertificateThumbprint '0123456789ABCDEF0123456789ABCDEF01234567' `
    -Force
```

The detached package signature covers package identity and all declared file
hashes. It is separate from Authenticode signing of the PowerShell module.

## Install on target machines

Run elevated:

```powershell
Install-EVXProviderPackage `
    -Path .\Contoso.Scanner-1.0.0.evxprovider `
    -TrustMode RequireTrustedSignature `
    -TrustedSignerThumbprint '0123456789ABCDEF0123456789ABCDEF01234567' `
    -Confirm:$false
```

Trust modes:

| Mode | Contract |
| --- | --- |
| `AllowUnsigned` | Unsigned packages are accepted; any present signature must still be valid |
| `RequireSignature` | A cryptographically valid package signature is required |
| `RequireTrustedSignature` | The signer must match an explicit thumbprint allowlist, or pass Windows code-signing chain trust when no list is supplied |

Exact signer pins support private enterprise certificates without granting
trust to every certificate from the same authority.

### Remote deployment

The package is an ordinary file:

```powershell
$session = New-PSSession -ComputerName APP01

Copy-Item .\Contoso.Scanner-1.0.0.evxprovider `
    -Destination C:\Windows\Temp\Contoso.Scanner.evxprovider `
    -ToSession $session

Invoke-Command -Session $session {
    Import-Module PSEventViewer
    Install-EVXProviderPackage `
        -Path C:\Windows\Temp\Contoso.Scanner.evxprovider `
        -TrustMode RequireTrustedSignature `
        -TrustedSignerThumbprint '0123456789ABCDEF0123456789ABCDEF01234567' `
        -Confirm:$false
}
```

The same file can be deployed by Intune, Configuration Manager, Group Policy,
an MSI/MSIX wrapper, or another software distribution system.

## Write by event and field name

Hashtable ordering is irrelevant. EventViewerX maps names to canonical
manifest order, validates required/unknown fields, converts native types, and
enforces Windows event-size limits.

```powershell
Write-EVXEvent `
    -ProviderName Contoso.Scanner `
    -EventName ScanCompleted `
    -Data @{
        FindingCount = 7
        ComputerName = $env:COMPUTERNAME
    } `
    -Confirm:$false
```

This is the normal PowerShell workflow. Callers do not need generated
PowerShell classes or positional payload knowledge.

## JSON definitions

JSON is useful when the schema is version-controlled or generated:

```powershell
Test-EVXProviderDefinition -Path .\provider.definition.json

New-EVXProviderPackage `
    -DefinitionPath .\provider.definition.json `
    -OutputPath .\provider.evxprovider
```

The JSON contract supports the same complete model as C#: explicit channels,
localized strings, levels, tasks, opcodes, keywords, value maps, bit maps,
event versions, field length/count references, and channel policy.
Explicit `null` values for required members or typed collections are returned
as structured validation errors by `Test-EVXProviderDefinition`; they do not
fall through as deserializer or null-reference failures.

Friendly event messages use `{FieldName}` placeholders. Windows reserves
percent sequences while rendering message resources, including `%n`, `%0`, and
`%%n`, so the validator rejects literal `%` characters in static message text
instead of publishing a message that renders incorrectly. Put percent-bearing
text such as `100%` or `literal %1` in a named string payload field; insertion
values are treated as data. Manifest symbols are normalized to uppercase ASCII
identifiers, so display names may still contain localized characters without
producing invalid C symbols.

## Field types

Friendly PowerShell aliases include `String`, `Int`, `UInt`, `Long`, `ULong`,
`DateTime`, and `Bool`. Native types include `UnicodeString`, `AnsiString`,
integer widths, `Float`, `Double`, `Boolean`, `Binary`, `Guid`, `Pointer`,
`FileTime`, `SystemTime`, `Sid`, and hexadecimal forms.

Use an output type or map when the raw native type needs semantic rendering.
Arrays and binary fields must declare a fixed dimension or reference an earlier
numeric count/length field. The validator rejects ambiguous layouts before any
manifest is compiled.

## Explicit channels and policy

Use the expanded schema when the conventional operational channel is not
enough:

```powershell
$provider = @{
    ProviderName = 'Contoso.Scanner'
    ProviderGuid = '7a87f315-4b5e-40a2-b748-b0cdd8adab41'
    Version      = '1.0.0'
    Channels     = @(
        @{
            Id               = 'Operational'
            Name             = 'Contoso.Scanner/Operational'
            Type             = 'Operational'
            Enabled          = $true
            MaximumSizeBytes = 67108864
            Retention        = $false
        },
        @{
            Id        = 'Analytic'
            Name      = 'Contoso.Scanner/Analytic'
            Type      = 'Analytic'
            Enabled   = $false
            Isolation = 'System'
        }
    )
    Events = @(
        @{
            Name    = 'ScanCompleted'
            Id      = 1000
            Channel = 'Operational'
            Version = 0
            Level   = 'win:Informational'
            Message = 'Scan of {ComputerName} found {FindingCount} issues.'
            Fields  = [ordered] @{
                ComputerName = 'String'
                FindingCount = 'UInt32'
            }
        }
    )
}
```

Use channel access SDDL only when the security requirement is understood and
tested. A provider package changes machine-wide event infrastructure.

## Typed C# schema and writes

Public payload properties become named fields. Explicit field-order attributes
keep the wire schema stable across source refactoring.

```csharp
using EventViewerX;
using EventViewerX.Providers;

public sealed class ScanCompletedPayload {
    [EventProviderPayloadField(0)]
    public string ComputerName { get; init; } = string.Empty;

    [EventProviderPayloadField(1)]
    public uint FindingCount { get; init; }
}

var provider = EventProviderDefinition.Create(
        "Contoso.Scanner",
        Guid.Parse("7a87f315-4b5e-40a2-b748-b0cdd8adab41"),
        "1.0.0")
    .AddChannel(EventProviderChannelDefinition.Operational(
        "Operational",
        "Contoso.Scanner/Operational"));

var scanCompleted =
    EventProviderEventDefinition.FromType<ScanCompletedPayload>(
        "ScanCompleted",
        1000,
        "Operational");

scanCompleted.Messages["en-US"] =
    "Scan of {ComputerName} found {FindingCount} issues.";
provider.AddEvent(scanCompleted);

EventProviderPackageBuildResult package =
    EventProviderPackageBuilder.Build(
        provider,
        "Contoso.Scanner-1.0.0.evxprovider");
```

On the elevated target:

```csharp
EventProviderPackageManager.Install(
    package.OutputPath,
    new EventProviderPackageInstallOptions {
        TrustMode =
            EventProviderPackageTrustMode.RequireTrustedSignature,
        TrustedSignerThumbprints =
            new[] { "0123456789ABCDEF0123456789ABCDEF01234567" }
    });
```

For repeated writes, open once:

```csharp
using var writer = ResolvedManifestEventWriter.Open(
    "Contoso.Scanner",
    "ScanCompleted");

writer.Write(new ScanCompletedPayload {
    ComputerName = Environment.MachineName,
    FindingCount = 7
});
```

The writer caches one native provider registration and resolves the active
EventViewerX-managed definition. A dictionary can be used when compile-time
payload types are not practical:

```csharp
writer.Write(new Dictionary<string, object?> {
    ["ComputerName"] = Environment.MachineName,
    ["FindingCount"] = 7U
});
```

The complete typed workflow is also compiled as
[`Examples.CustomProviders.cs`](../Sources/EventViewerX.Examples/Examples.CustomProviders.cs).

## Versioning and compatibility

Provider name and GUID are permanent identities. A released `(EventId,
Version)` is also immutable.

Create a new event version when changing:

- field order, name, native type, length, or count;
- map identity or output semantics;
- channel, level, task, opcode, keyword, or descriptor identity.

Compatibility checks preserve the exact casing of manifest identifiers.
Changing `UserName` to `username`, for example, requires a new event version
because named `EventData` filters and downstream parsers can be case-sensitive.

Build upgrades against the released package:

```powershell
New-EVXProviderPackage `
    -Definition $providerV11 `
    -BaselinePath .\Contoso.Scanner-1.0.0.evxprovider `
    -OutputPath .\Contoso.Scanner-1.1.0.evxprovider
```

Compatibility validation rejects schema rewrites and removal of metadata
needed to render historical records.

## Upgrade, repair, rollback, and inventory

Installation is transactional:

```powershell
Install-EVXProviderPackage `
    .\Contoso.Scanner-1.1.0.evxprovider `
    -Confirm:$false
```

The package is verified and staged before the old registration is changed. A
failed activation restores the previous provider. Reinstalling the exact
active package repairs missing registration or modified extracted files and
reports `Repaired`.

```powershell
Get-EVXProvider -InstalledPackage |
    Where-Object ProviderName -eq Contoso.Scanner |
    Select-Object ProviderName, PackageVersion, IsActive, IsRegistered,
        IsSigned, SignerThumbprint, PackagePath
```

Old versions remain retained and can be reinstalled. Downgrade requires an
explicit `-AllowDowngrade`.

Released versions are immutable by default. For an intentional development
replacement with different bytes and the same version:

```powershell
Install-EVXProviderPackage `
    .\Contoso.Scanner-1.0.0.evxprovider `
    -AllowSameVersionReplacement `
    -Confirm:$false
```

Prefer a new version for every published package.

## Uninstall

```powershell
Uninstall-EVXProviderPackage `
    -ProviderName Contoso.Scanner `
    -Confirm:$false
```

By default, package resources are retained so historical EVTX messages remain
renderable. `-RemoveFiles` requests complete removal. Windows may keep a
message resource DLL mapped briefly; the result then reports
`FileRemovalPendingReboot = $true` and schedules safe removal.

## Package contents and security boundary

An `.evxprovider` is a constrained ZIP:

- `provider.definition.json`: complete typed source of truth;
- `provider.man`: generated Windows instrumentation manifest;
- `provider.resources.dll`: message/schema resources, with no code entry point;
- `schema-lock.json`: compatibility-critical identity snapshot;
- `package.json`: identity, managed-compiler provenance, and SHA-256 of every
  payload file.

Every activation is extracted from verified package bytes into a fresh,
restricted directory. `SYSTEM` and Administrators receive full control; Local
Service and local Users receive read/execute only. Lifecycle changes for the
same provider GUID are serialized across processes.

The default installation root is a dedicated EventViewerX directory under
ProgramData. A custom C# root must also be dedicated to EventViewerX; the
installer refuses to claim a directory containing unrelated content and sets
the managed ACL throughout the tree.

## CI/CD recommendation

- Store the definition and last released `.evxprovider` baseline in controlled
  release inputs.
- Validate on every change.
- Build once on a Windows worker with the pinned EventViewerX/PSEventViewer
  package version.
- Sign the package and publish its SHA-256.
- Test install, named write, read-back, upgrade, repair, rollback-on-failure,
  and uninstall on a disposable Windows machine.
- Deploy the exact package to targets with `RequireTrustedSignature` and
  explicit signer pins.
- Never rebuild a released package separately on each target.
