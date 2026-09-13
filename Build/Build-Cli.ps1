[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Build')]
    [string] $RunMode = 'Build'
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force -ErrorAction Stop

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$invokeSplat = @{
    ConfigPath = Join-Path $PSScriptRoot 'release.json'
    ToolsOnly  = $true
    StageRoot  = Join-Path $repositoryRoot 'Artefacts\UploadReady\Cli'
}
if ($RunMode -eq 'Plan') {
    $invokeSplat.Plan = $true
}

Invoke-PowerForgeRelease @invokeSplat
