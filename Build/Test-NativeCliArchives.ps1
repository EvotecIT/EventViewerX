<#
.SYNOPSIS
Builds and checks both CLI archive styles on a matching native host.
.DESCRIPTION
Rejects a different operating system or process architecture before building.
Delegates archive production to PowerForge and runtime checks to the shared
release validator, and writes compact JSON evidence for the checked archives.
.PARAMETER RuntimeIdentifier
The configured runtime matching this host, such as linux-arm64.
.PARAMETER WorkRoot
A new caller-owned directory for extracted archives and SQLite databases.
.PARAMETER EvidencePath
The JSON file to receive host details, archive hashes, and validation results.
.EXAMPLE
./Build/Test-NativeCliArchives.ps1 -RuntimeIdentifier win-x64 -WorkRoot ./Ignore/NativeArchives -EvidencePath ./Ignore/native-win-x64.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^(win|linux|osx)-(x64|arm64)$')]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory)] [string] $WorkRoot,
    [Parameter(Mandatory)] [string] $EvidencePath
)

$ErrorActionPreference = 'Stop'
$runtimeParts = $RuntimeIdentifier.Split('-')
$osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$hostPlatform = if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    'win'
} elseif ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)) {
    'linux'
} elseif ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::OSX)) {
    'osx'
} else {
    throw 'The current operating system has no configured CLI archive.'
}
if ($runtimeParts[0] -ne $hostPlatform -or $runtimeParts[1] -ne $osArchitecture -or
    $runtimeParts[1] -ne $processArchitecture) {
    throw "Native qualification requires $RuntimeIdentifier; host is $hostPlatform-$osArchitecture and process is $processArchitecture."
}
if (Test-Path -LiteralPath $WorkRoot) { throw 'Use a new workspace for native archive qualification.' }

$plan = & (Join-Path $PSScriptRoot 'Build-Cli.ps1') -RunMode Plan -Runtimes $RuntimeIdentifier
if (-not $plan.Success -or @($plan.DotNetToolPlan.Targets).Count -ne 1 -or
    @($plan.DotNetToolPlan.Targets.Combinations).Count -ne 2) {
    throw 'Native qualification requires one CLI target with two archive styles.'
}
$target = $plan.DotNetToolPlan.Targets[0]
$build = & (Join-Path $PSScriptRoot 'Build-Cli.ps1') -RunMode Build -Runtimes $RuntimeIdentifier
if (-not $build.Success) { throw 'PowerForge CLI archive build failed.' }

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceRevision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the checked source revision.' }
$archiveRoot = Join-Path $repositoryRoot 'Artefacts/UploadReady/Cli/cli'
$evidence = foreach ($combination in $target.Combinations) {
    $style = $combination.Style.ToString()
    $archivePath = Join-Path $archiveRoot "$($target.Name)-$($combination.Framework)-$RuntimeIdentifier-$style.zip"
    $result = & (Join-Path $PSScriptRoot 'Test-CliArchive.ps1') -ArchivePath $archivePath `
        -WorkRoot (Join-Path $WorkRoot $style) -Version $target.Version
    if (-not $result.Success) { throw "The $RuntimeIdentifier $style archive failed runtime validation." }
    [pscustomobject] @{
        RuntimeIdentifier = $RuntimeIdentifier
        OS = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        OSArchitecture = $osArchitecture
        ProcessArchitecture = $processArchitecture
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        PSPublishModuleVersion = (Get-Module PSPublishModule).Version.ToString()
        SourceRevision = [string] $sourceRevision
        Style = $style
        Version = $target.Version
        ArchiveName = [IO.Path]::GetFileName($archivePath)
        ArchiveSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
        Success = $result.Success
        Checks = @($result.Checks)
        Errors = @($result.Errors)
    }
}
$evidenceDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($EvidencePath))
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $EvidencePath -Encoding UTF8
$evidence
