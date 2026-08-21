param(
    [ValidateSet('Plan', 'Build')]
    [string] $RunMode = 'Build',

    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $Runtime = @('win-x64', 'win-arm64'),

    [ValidateSet('FrameworkDependent', 'PortableCompat')]
    [string[]] $Style = @('FrameworkDependent', 'PortableCompat')
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artefactRoot = Join-Path $repositoryRoot 'Artefacts\Cli'
$releaseRoot = Join-Path $repositoryRoot 'Artefacts\UploadReady\Cli'

$target = New-ConfigurationProjectTarget `
    -Name 'EventViewerX.Cli' `
    -ProjectPath 'Sources\EventViewerX.Cli\EventViewerX.Cli.csproj' `
    -Kind Cli `
    -Framework 'net10.0-windows' `
    -Runtimes $Runtime `
    -Styles $Style `
    -OutputType Tool `
    -Zip

$release = New-ConfigurationProjectRelease -Configuration 'Release' -ToolOutput Tool
$output = New-ConfigurationProjectOutput `
    -OutputRoot $artefactRoot `
    -StageRoot $releaseRoot `
    -ChecksumsPath (Join-Path $releaseRoot 'EventViewerX.Cli-SHA256SUMS.txt')

$project = New-ConfigurationProject `
    -Name 'EventViewerX.Cli' `
    -ProjectRoot $repositoryRoot `
    -Release $release `
    -Output $output `
    -Target $target

$invokeSplat = @{
    Project = $project
}
if ($RunMode -eq 'Plan') {
    $invokeSplat.Plan = $true
}

$powerForgeWorkingPath = Join-Path $repositoryRoot 'Artefacts'
New-Item -ItemType Directory -Path $powerForgeWorkingPath -Force | Out-Null
Push-Location -LiteralPath $powerForgeWorkingPath
try {
    Invoke-ProjectRelease @invokeSplat
} finally {
    Pop-Location
}
