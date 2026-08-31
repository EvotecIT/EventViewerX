param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $true,

    [switch] $SkipCli
)

$ErrorActionPreference = 'Stop'

$moduleBuildSplat = @{
    RunMode    = $RunMode
    SignModule = $SignModule
}
& (Join-Path $PSScriptRoot 'Build-Module.ps1') @moduleBuildSplat

if (-not $SkipCli -and $RunMode -ne 'Manifest') {
    & (Join-Path $PSScriptRoot 'Test-ModuleRuntime.ps1')
    & (Join-Path $PSScriptRoot 'Build-Cli.ps1')
    & (Join-Path $PSScriptRoot 'Test-ReleaseArchitecture.ps1')
}
