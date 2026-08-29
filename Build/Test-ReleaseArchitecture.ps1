param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$packageRoot = Join-Path $RepositoryRoot 'Artefacts\ProjectBuild\packages'
$moduleRoot = Join-Path $RepositoryRoot 'Artefacts\Unpacked\Modules\PSEventViewer'
$cliManifestPath = Join-Path $RepositoryRoot 'Artefacts\UploadReady\Cli\release-manifest.json'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedPackages = @(
    'EventViewerX'
    'EventViewerX.Detection'
    'EventViewerX.Evtx'
    'EventViewerX.Reporting'
    'EventViewerX.Storage'
)
$packageMetadata = @{}
foreach ($packageId in $expectedPackages) {
    $packagePattern = '^' + [regex]::Escape($packageId) + '\.\d'
    [array] $packages = Get-ChildItem -LiteralPath $packageRoot -Filter "$packageId.*.nupkg" -File |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' -and $_.Name -match $packagePattern }
    if ($packages.Count -ne 1) {
        throw "Expected exactly one package for $packageId, found $($packages.Count)."
    }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        if (-not $entry) {
            throw "Package '$($packages[0].Name)' does not contain a nuspec."
        }
        $stream = $entry.Open()
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    } finally {
        $archive.Dispose()
    }
    [array] $dependencies = $nuspec.package.metadata.dependencies.group.dependency |
        ForEach-Object { [string] $_.id } | Sort-Object -Unique
    $packageMetadata[$packageId] = [pscustomobject] @{
        Version = [string] $nuspec.package.metadata.version
        Dependencies = $dependencies
    }
}

[array] $versions = $packageMetadata.Values.Version | Sort-Object -Unique
if ($versions.Count -ne 1) {
    throw "EventViewerX package versions are not synchronized: $($versions -join ', ')."
}
$version = $versions[0]

function Assert-PackageBoundary {
    param(
        [string] $PackageId,
        [string[]] $Required,
        [string[]] $Forbidden
    )

    [array] $dependencies = $packageMetadata[$PackageId].Dependencies
    foreach ($dependency in $Required) {
        if ($dependency -notin $dependencies) {
            throw "$PackageId is missing required dependency $dependency."
        }
    }
    foreach ($dependency in $Forbidden) {
        if ($dependency -in $dependencies) {
            throw "$PackageId must not depend on $dependency."
        }
    }
}

Assert-PackageBoundary -PackageId 'EventViewerX' -Required @() `
    -Forbidden @('EventViewerX.Detection', 'EventViewerX.Evtx', 'EventViewerX.Reporting', 'EventViewerX.Storage')
Assert-PackageBoundary -PackageId 'EventViewerX.Detection' -Required @('EventViewerX') `
    -Forbidden @('EventViewerX.Evtx', 'EventViewerX.Reporting', 'EventViewerX.Storage')
Assert-PackageBoundary -PackageId 'EventViewerX.Evtx' -Required @('EventViewerX') `
    -Forbidden @('EventViewerX.Detection', 'EventViewerX.Reporting', 'EventViewerX.Storage')
Assert-PackageBoundary -PackageId 'EventViewerX.Reporting' -Required @('EventViewerX') `
    -Forbidden @('EventViewerX.Detection', 'EventViewerX.Evtx', 'EventViewerX.Storage')
Assert-PackageBoundary -PackageId 'EventViewerX.Storage' -Required @('EventViewerX') `
    -Forbidden @('EventViewerX.Detection', 'EventViewerX.Evtx', 'EventViewerX.Reporting')

$manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $moduleRoot 'PSEventViewer.psd1')
if ([version] $manifest.ModuleVersion -ne [version] $version) {
    throw "PSEventViewer $($manifest.ModuleVersion) does not match package version $version."
}
[array] $moduleAssemblies = Get-ChildItem -LiteralPath (Join-Path $moduleRoot 'Lib\Standard') `
    -Filter 'EventViewerX*.dll' -File
foreach ($assembly in $moduleAssemblies) {
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($assembly.FullName).Version
    if ($assemblyVersion.Major -ne ([version] $version).Major -or
        $assemblyVersion.Minor -ne ([version] $version).Minor -or
        $assemblyVersion.Build -ne ([version] $version).Build) {
        throw "Module assembly '$($assembly.Name)' version $assemblyVersion does not match $version."
    }
}

$cliManifest = Get-Content -LiteralPath $cliManifestPath -Raw | ConvertFrom-Json
[array] $cliEntries = $cliManifest.assetEntries | Where-Object { $_.category -eq 'Tool' }
if ($cliEntries.Count -ne 12) {
    throw "Expected 12 CLI runtime/style assets, found $($cliEntries.Count)."
}
if (@($cliEntries | Where-Object { $_.Version -ne $version }).Count -ne 0) {
    throw "One or more CLI assets do not match release version $version."
}

[pscustomobject] @{
    Version = $version
    Packages = $expectedPackages.Count
    ModuleAssemblies = $moduleAssemblies.Count
    CliAssets = $cliEntries.Count
    StorageDependsOnReporting = $packageMetadata['EventViewerX.Storage'].Dependencies -contains 'EventViewerX.Reporting'
} | ConvertTo-Json -Compress
