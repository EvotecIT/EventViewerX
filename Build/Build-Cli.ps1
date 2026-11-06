[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Build')]
    [string] $RunMode = 'Build',

    [ValidateNotNullOrEmpty()]
    [string] $Version,

    [ValidateNotNullOrEmpty()]
    [string[]] $Runtimes
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force -ErrorAction Stop

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Import-PowerShellDataFile (Join-Path $repositoryRoot 'PSEventViewer.psd1')).ModuleVersion
}
$invokeSplat = @{
    ConfigPath = Join-Path $PSScriptRoot 'release.json'
    ToolsOnly  = $true
    ReleaseVersion = $Version
    StageRoot  = Join-Path $repositoryRoot 'Artefacts\UploadReady\Cli'
}
if ($RunMode -eq 'Plan') {
    $invokeSplat.Plan = $true
}
if ($PSBoundParameters.ContainsKey('Runtimes')) {
    $invokeSplat.Runtimes = $Runtimes
}

Invoke-PowerForgeRelease @invokeSplat
