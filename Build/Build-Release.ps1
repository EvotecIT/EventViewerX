[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [string] $ConfigPath = (Join-Path $PSScriptRoot 'release.json')
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force -ErrorAction Stop

$invokeSplat = @{
    ConfigPath    = $ConfigPath
    ModuleRunMode = if ($RunMode -eq 'Build') { 'Build' } else { 'Publish' }
}
if ($RunMode -eq 'Plan') {
    $invokeSplat.Plan = $true
}

Invoke-PowerForgeRelease @invokeSplat
