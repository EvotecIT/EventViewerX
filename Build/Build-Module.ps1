[CmdletBinding()]
param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Documentation', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $true,

    [ValidatePattern('^\d+\.\d+\.(?:\d+|X)$')]
    [string] $ModuleVersion = '4.0.X',

    [string] $PreReleaseTag,

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateSet('auto', 'net472', 'net8.0', 'net10.0')]
    [string] $Framework,

    [switch] $NoDotnetBuild,

    [string] $StagingPath,

    [switch] $ReuseStaging,

    [bool] $IncludeProjectPackages = $true,

    [bool] $IncludeModulePublishing = $true,

    [switch] $PowerForgeUnifiedGitHubRelease,

    [switch] $PowerForgeReleaseStage,

    [switch] $SkipInstall
)

$ErrorActionPreference = 'Stop'

if ($RunMode -eq 'Publish') {
    throw 'Direct module publication is disabled. Use Build-Release.ps1 -RunMode Publish so commit, checkout, confirmation, and staged-artifact guards are enforced.'
}

Import-Module PSPublishModule -Force

$moduleBuildSplat = @{
    ConfigPath              = Join-Path $PSScriptRoot 'module.json'
    RunMode                 = $RunMode
    ModuleVersion           = $ModuleVersion
    BuildConfiguration      = $Configuration
    IncludeProjectPackages  = $IncludeProjectPackages
    IncludeModulePublishing = $IncludeModulePublishing
}
if (-not [string]::IsNullOrWhiteSpace($PreReleaseTag)) {
    $moduleBuildSplat.PreReleaseTag = $PreReleaseTag
}
if ($PSBoundParameters.ContainsKey('Framework')) {
    $moduleBuildSplat.BuildFramework = $Framework
}
if ($NoDotnetBuild) {
    $moduleBuildSplat.NoDotnetBuild = $true
}
if (-not [string]::IsNullOrWhiteSpace($StagingPath)) {
    $moduleBuildSplat.StagingPath = $StagingPath
}
if ($ReuseStaging -or ($NoDotnetBuild -and -not [string]::IsNullOrWhiteSpace($StagingPath))) {
    $moduleBuildSplat.ReuseStaging = $true
}
if ($SignModule) {
    $moduleBuildSplat.SignModule = $true
} else {
    $moduleBuildSplat.NoSign = $true
}
if ($PowerForgeUnifiedGitHubRelease) {
    $moduleBuildSplat.PowerForgeUnifiedGitHubRelease = $true
}
if ($SkipInstall) {
    $moduleBuildSplat.SkipInstall = $true
}

Invoke-ModuleBuild @moduleBuildSplat
