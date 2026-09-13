[CmdletBinding()]
param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Plan', 'Build', 'Publish')]
    [string] $RunMode = 'Build'
)

$ErrorActionPreference = 'Stop'

if ($RunMode -eq 'Manifest') {
    & (Join-Path $PSScriptRoot 'Build-Module.ps1') -RunMode Manifest
} else {
    & (Join-Path $PSScriptRoot 'Build-Release.ps1') -RunMode $RunMode
}
