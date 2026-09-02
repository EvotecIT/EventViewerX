param(
    [ValidateSet('Plan', 'Build')]
    [string] $RunMode = 'Build',

    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string[]] $Runtime = @(
        'win-x64'
        'win-arm64'
        'linux-x64'
        'linux-arm64'
        'osx-x64'
        'osx-arm64'
    ),

    [ValidateSet('FrameworkDependent', 'PortableCompat')]
    [string[]] $Style = @('FrameworkDependent', 'PortableCompat')
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artefactRoot = Join-Path $repositoryRoot 'Artefacts\Cli'
$releaseRoot = Join-Path $repositoryRoot 'Artefacts\UploadReady\Cli'

$invokeSplat = @{
    ConfigPath = (Join-Path $PSScriptRoot 'release.json')
    ToolsOnly  = $true
    Runtimes   = $Runtime
    Styles     = $Style
    OutputRoot = $artefactRoot
    StageRoot  = $releaseRoot
}
if ($RunMode -eq 'Plan') {
    $invokeSplat.Plan = $true
}

Invoke-PowerForgeRelease @invokeSplat
