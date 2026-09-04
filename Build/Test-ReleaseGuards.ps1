[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildModulePath = Join-Path $PSScriptRoot 'Build-Module.ps1'
$buildReleasePath = Join-Path $PSScriptRoot 'Build-Release.ps1'

try {
    & $buildModulePath -RunMode Publish -SignModule:$false
    throw 'Build-Module.ps1 unexpectedly accepted direct publication.'
} catch {
    if ($_.Exception.Message -notlike 'Direct module publication is disabled.*') {
        throw
    }
}

[string] $actualCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($actualCommit)) {
    throw 'The current commit could not be resolved for release-guard validation.'
}

$markerPath = Join-Path $repositoryRoot '.release-guard-untracked'
try {
    Set-Content -LiteralPath $markerPath -Value 'release guard validation'
    try {
        & $buildReleasePath -RunMode Publish -Version '4.0.0' `
            -SignModule:$false -ExpectedCommit $actualCommit `
            -Confirmation "publish:4.0.0:$actualCommit"
        throw 'Build-Release.ps1 unexpectedly accepted an untracked file.'
    } catch {
        if ($_.Exception.Message -notlike '*release checkout contains changes*' -or
            $_.Exception.Message -notlike '*.release-guard-untracked*') {
            throw
        }
    }
} finally {
    if (Test-Path -LiteralPath $markerPath) {
        Remove-Item -LiteralPath $markerPath -Force
    }
}

[pscustomobject] @{
    DirectModulePublishBlocked = $true
    UntrackedFileBlocked = $true
}
