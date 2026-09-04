[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '4.0.0',

    [bool] $SignModule = $true,

    [ValidateSet('auto', 'net472', 'net8.0', 'net10.0')]
    [string] $ModuleFramework,

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $ExpectedCommit,

    [string] $Confirmation
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..'))
$configPath = Join-Path $PSScriptRoot 'release.json'

if ($RunMode -eq 'Publish') {
    if ([string]::IsNullOrWhiteSpace($ExpectedCommit)) {
        throw 'ExpectedCommit is required for a public release.'
    }
    [string] $actualCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualCommit -ine $ExpectedCommit) {
        throw "Expected release commit '$ExpectedCommit', received '$actualCommit'."
    }
    [array] $checkoutChanges = & git -C $repositoryRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw 'The release checkout state could not be inspected.'
    }
    if ($checkoutChanges.Count -ne 0) {
        throw "The release checkout contains changes: $($checkoutChanges -join ', ')"
    }
    [string] $expectedConfirmation =
        "publish:$Version`:$($ExpectedCommit.ToLowerInvariant())"
    if ($Confirmation -cne $expectedConfirmation) {
        throw "Confirmation must exactly equal '$expectedConfirmation'."
    }
}

Import-Module PSPublishModule -Force

$invokeSplat = @{
    ConfigPath       = $configPath
    ModuleVersion    = $Version
    ModuleSignModule = $SignModule
}
if ($PSBoundParameters.ContainsKey('ModuleFramework')) {
    $invokeSplat.ModuleFramework = $ModuleFramework
}
switch ($RunMode) {
    'Plan' {
        $invokeSplat.Plan = $true
        $invokeSplat.ModuleRunMode = 'Publish'
        $invokeSplat.PublishNuget = $true
    }
    'Build' {
        $invokeSplat.ModuleRunMode = 'Build'
    }
    'Publish' {
        $invokeSplat.ModuleRunMode = 'Publish'
        $invokeSplat.PublishNuget = $true
    }
}

$result = Invoke-PowerForgeRelease @invokeSplat
if ($null -eq $result -or -not $result.Success) {
    $message = if ($null -eq $result) {
        'The unified PowerForge release returned no result.'
    } else {
        [string] $result.ErrorMessage
    }
    throw "The unified PowerForge release failed. $message"
}

$result
