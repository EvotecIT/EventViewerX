[CmdletBinding()]
param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Documentation', 'Plan', 'Build', 'Publish')]
    [string] $RunMode = 'Build'
)

$ErrorActionPreference = 'Stop'

if ($RunMode -in @('Plan', 'Build', 'Publish')) {
    $releaseSplat = @{
        RunMode    = $RunMode
        ConfigPath = Join-Path $PSScriptRoot 'release.module.json'
    }
    & (Join-Path $PSScriptRoot 'Build-Release.ps1') @releaseSplat
} else {
    Import-Module PSPublishModule -Force -ErrorAction Stop
    Invoke-ModuleBuild `
        -ConfigPath (Join-Path $PSScriptRoot 'module.json') `
        -RunMode $RunMode
}
