[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $CliManifestPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'Test-ReleaseConfiguration.ps1') `
    -RepositoryRoot $repositoryRoot | Out-Null
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
$moduleValidationRoot = $null
if ($null -ne $context) {
    if ([string]::IsNullOrWhiteSpace($CliManifestPath) -and
        -not [string]::IsNullOrWhiteSpace([string] $context.ReleaseManifestPath)) {
        $CliManifestPath = [string] $context.ReleaseManifestPath
    }

    [array] $modulePackages = @($context.StagedAssets | Where-Object {
        [System.IO.Path]::GetFileName([string] $_) -ieq "PSEventViewer.v$Version.zip"
    })
    if ($modulePackages.Count -ne 1) {
        throw "Expected one staged PSEventViewer $Version archive, found $($modulePackages.Count)."
    }

    [array] $cliPackages = @($context.StagedAssets | Where-Object {
        [System.IO.Path]::GetFileName([string] $_) -ieq "EventViewerX.Cli.$Version.nupkg"
    })
    if ($cliPackages.Count -ne 1) {
        throw "Expected one staged EventViewerX.Cli $Version package, found $($cliPackages.Count)."
    }
    $packageRoot = Split-Path -Parent ([string] $cliPackages[0])

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $validationRoot = Join-Path $repositoryRoot 'Artefacts\Validation'
    $moduleValidationRoot = Join-Path $validationRoot `
        "StagedModule-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $moduleValidationRoot -Force | Out-Null
    try {
        [System.IO.Compression.ZipFile]::ExtractToDirectory(
            [string] $modulePackages[0],
            $moduleValidationRoot
        )
    } catch {
        Remove-Item -LiteralPath $moduleValidationRoot -Recurse -Force
        throw
    }
    $moduleRoot = Join-Path $moduleValidationRoot 'PSEventViewer'
}

try {
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
} finally {
    if (-not [string]::IsNullOrWhiteSpace($moduleValidationRoot) -and
        (Test-Path -LiteralPath $moduleValidationRoot)) {
        Remove-Item -LiteralPath $moduleValidationRoot -Recurse -Force
    }
}
