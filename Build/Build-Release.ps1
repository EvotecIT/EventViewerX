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

$originalGitHubToken = $env:GITHUB_TOKEN
$injectedGitHubToken = $false
try {
    if ($RunMode -eq 'Publish' -and [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        $token = $env:GH_TOKEN
        if ([string]::IsNullOrWhiteSpace($token) -and (Get-Command gh -ErrorAction SilentlyContinue)) {
            $token = & gh auth token 2>$null
            if ($LASTEXITCODE -ne 0) { $token = $null }
        }
        if ([string]::IsNullOrWhiteSpace($token)) {
            throw 'GitHub release publishing requires GITHUB_TOKEN, GH_TOKEN, or an authenticated gh CLI.'
        }
        $env:GITHUB_TOKEN = $token.Trim()
        $injectedGitHubToken = $true
    }

    Invoke-PowerForgeRelease @invokeSplat
} finally {
    if ($injectedGitHubToken) {
        if ($null -eq $originalGitHubToken) {
            Remove-Item Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
        } else {
            $env:GITHUB_TOKEN = $originalGitHubToken
        }
    }
}
