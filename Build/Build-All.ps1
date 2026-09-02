param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $true,

    [ValidateSet('auto', 'net472', 'net8.0', 'net10.0')]
    [string] $ModuleFramework,

    [switch] $SkipCli,

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $ExpectedCommit,

    [string] $PublishConfirmation
)

$ErrorActionPreference = 'Stop'

if ($RunMode -eq 'Publish') {
    $releaseSplat = @{
        RunMode        = 'Publish'
        SignModule     = $SignModule
        ExpectedCommit = $ExpectedCommit
        Confirmation   = $PublishConfirmation
    }
    if ($PSBoundParameters.ContainsKey('ModuleFramework')) {
        $releaseSplat.ModuleFramework = $ModuleFramework
    }
    & (Join-Path $PSScriptRoot 'Build-Release.ps1') @releaseSplat
    exit 0
}

$moduleBuildSplat = @{
    RunMode    = $RunMode
    SignModule = $SignModule
}
& (Join-Path $PSScriptRoot 'Build-Module.ps1') @moduleBuildSplat

if (-not $SkipCli -and $RunMode -ne 'Manifest') {
    & (Join-Path $PSScriptRoot 'Test-ModuleRuntime.ps1')
    & (Join-Path $PSScriptRoot 'Test-CliPackage.ps1')
    & (Join-Path $PSScriptRoot 'Build-Cli.ps1')
    & (Join-Path $PSScriptRoot 'Test-ReleaseArchitecture.ps1')
}
