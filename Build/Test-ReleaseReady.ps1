[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $CliManifestPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$context = $null
if (-not [string]::IsNullOrWhiteSpace($env:POWERFORGE_CONTEXT) -and
    (Test-Path -LiteralPath $env:POWERFORGE_CONTEXT -PathType Leaf)) {
    $context = Get-Content -LiteralPath $env:POWERFORGE_CONTEXT -Raw |
        ConvertFrom-Json
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    if ($null -eq $context) {
        throw 'Version is required outside a PowerForge lifecycle action.'
    }
    $Version = [string] $context.ResolvedVersion
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'The release version could not be resolved.'
}

$moduleRoot = $null
$packageRoot = $null
if ($null -ne $context) {
    if (-not [string]::IsNullOrWhiteSpace([string] $context.ModuleStagingPath)) {
        $moduleRoot = [string] $context.ModuleStagingPath
    }
    if ([string]::IsNullOrWhiteSpace($CliManifestPath) -and
        -not [string]::IsNullOrWhiteSpace([string] $context.ReleaseManifestPath)) {
        $CliManifestPath = [string] $context.ReleaseManifestPath
    }

    [array] $cliPackages = @($context.StagedAssets | Where-Object {
        [System.IO.Path]::GetFileName([string] $_) -ieq "EventViewerX.Cli.$Version.nupkg"
    })
    if ($cliPackages.Count -ne 1) {
        throw "Expected one staged EventViewerX.Cli $Version package, found $($cliPackages.Count)."
    }
    $packageRoot = Split-Path -Parent ([string] $cliPackages[0])
}

$moduleRuntimeSplat = @{}
if (-not [string]::IsNullOrWhiteSpace($moduleRoot)) {
    $moduleRuntimeSplat.ModulePath = $moduleRoot
}
& (Join-Path $PSScriptRoot 'Test-ModuleRuntime.ps1') @moduleRuntimeSplat

$cliPackageSplat = @{ Version = $Version }
if (-not [string]::IsNullOrWhiteSpace($packageRoot)) {
    $cliPackageSplat.PackageRoot = $packageRoot
}
& (Join-Path $PSScriptRoot 'Test-CliPackage.ps1') @cliPackageSplat

$architectureSplat = @{ RepositoryRoot = $repositoryRoot }
if (-not [string]::IsNullOrWhiteSpace($packageRoot)) {
    $architectureSplat.PackageRoot = $packageRoot
}
if (-not [string]::IsNullOrWhiteSpace($moduleRoot)) {
    $architectureSplat.ModuleRoot = $moduleRoot
}
if (-not [string]::IsNullOrWhiteSpace($CliManifestPath)) {
    $architectureSplat.CliManifestPath = $CliManifestPath
}
& (Join-Path $PSScriptRoot 'Test-ReleaseArchitecture.ps1') @architectureSplat
