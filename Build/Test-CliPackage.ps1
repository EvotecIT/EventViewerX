[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string] $PackageRoot,

    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = '4.0.0'
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path $RepositoryRoot 'Artefacts\ProjectBuild\packages'
}
$PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
$packagePath = Join-Path $PackageRoot "EventViewerX.Cli.$Version.nupkg"
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "The EventViewerX.Cli $Version package was not found at '$packagePath'."
}

$validationRoot = Join-Path $RepositoryRoot 'Artefacts\Validation'
$toolRoot = Join-Path $validationRoot "CliTool-$([guid]::NewGuid().ToString('N'))"
$nugetConfigPath = Join-Path $toolRoot 'NuGet.config'
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null

try {
    $escapedPackageRoot = [System.Security.SecurityElement]::Escape($PackageRoot)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="release-packages" value="$escapedPackageRoot" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfigPath -Encoding UTF8

    & dotnet tool install `
        --tool-path $toolRoot `
        --configfile $nugetConfigPath `
        --no-cache `
        EventViewerX.Cli `
        --version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Installing EventViewerX.Cli $Version from the staged packages failed."
    }

    $toolFileName = if ($env:OS -eq 'Windows_NT') { 'evx.exe' } else { 'evx' }
    $toolPath = Join-Path $toolRoot $toolFileName
    if (-not (Test-Path -LiteralPath $toolPath -PathType Leaf)) {
        throw "The installed package did not expose the expected '$toolFileName' command."
    }

    [array] $versionOutput = & $toolPath --version
    if ($LASTEXITCODE -ne 0 -or ($versionOutput -join "`n").Trim() -ne $Version) {
        throw "The installed CLI did not report version $Version."
    }

    [array] $helpOutput = & $toolPath --help
    [string] $helpText = $helpOutput -join "`n"
    if ($LASTEXITCODE -ne 0 -or
        $helpText -notmatch [regex]::Escape("EventViewerX $Version") -or
        $helpText -notmatch [regex]::Escape('evx query')) {
        throw 'The installed CLI help contract failed.'
    }

    [array] $typesOutput = & $toolPath types
    if ($LASTEXITCODE -ne 0) {
        throw 'The installed CLI could not enumerate its canonical event types.'
    }
    [array] $types = $typesOutput | ConvertFrom-Json
    if (@($types).Count -eq 0) {
        throw 'The installed CLI returned no canonical event types.'
    }

    [pscustomobject] @{
        Package = [System.IO.Path]::GetFileName($packagePath)
        Version = $Version
        Command = $toolFileName
        EventTypes = @($types).Count
    } | ConvertTo-Json -Compress
} finally {
    if (Test-Path -LiteralPath $toolRoot) {
        Remove-Item -LiteralPath $toolRoot -Recurse -Force
    }
}
