[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

$release = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Build\release.json') -Raw |
    ConvertFrom-Json
if ([string] $release.GitHub.TokenEnvName -ne 'GITHUB_TOKEN' -or
    -not [string]::IsNullOrWhiteSpace([string] $release.GitHub.TokenFilePath)) {
    throw 'The unified GitHub release must use GITHUB_TOKEN without a machine-specific token path.'
}

$projectBuild = Get-Content -LiteralPath `
    (Join-Path $RepositoryRoot 'Sources\Build\project.build.json') -Raw |
    ConvertFrom-Json
if ([string] $projectBuild.PublishApiKeyEnvName -ne 'NUGET_API_KEY' -or
    -not [string]::IsNullOrWhiteSpace([string] $projectBuild.PublishApiKeyFilePath)) {
    throw 'NuGet publication must use NUGET_API_KEY without a machine-specific API-key path.'
}
if ([string] $projectBuild.GitHubAccessTokenEnvName -ne 'GITHUB_TOKEN' -or
    -not [string]::IsNullOrWhiteSpace([string] $projectBuild.GitHubAccessTokenFilePath)) {
    throw 'Project GitHub authentication must use GITHUB_TOKEN without a machine-specific token path.'
}

$moduleBuild = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Build\module.json') -Raw |
    ConvertFrom-Json
[array] $gallerySegments = @($moduleBuild.Segments | Where-Object {
    $_.Type -eq 'GalleryNuget' -and $_.Configuration.Enabled -eq $true
})
if ($gallerySegments.Count -ne 1) {
    throw "Expected one enabled PowerShell Gallery publication segment, found $($gallerySegments.Count)."
}
$galleryKeyPath = [string] $gallerySegments[0].Configuration.ApiKeyFilePath
if ([System.IO.Path]::IsPathRooted($galleryKeyPath) -or
    $galleryKeyPath -ne '../.secrets/PowerShellGalleryAPI.txt') {
    throw 'PowerShell Gallery publication must use the ignored repo-local .secrets key path.'
}

[array] $legacyGitHubSegments = @($moduleBuild.Segments | Where-Object {
    $_.Type -eq 'GitHubNuget'
})
if (@($legacyGitHubSegments | Where-Object {
    $_.Configuration.Enabled -eq $true -or
    -not [string]::IsNullOrWhiteSpace([string] $_.Configuration.ApiKeyFilePath)
}).Count -ne 0) {
    throw 'The legacy module GitHub lane must stay disabled and must not declare a token-file path.'
}

[pscustomobject] @{
    NuGetCredential = 'NUGET_API_KEY'
    GitHubCredential = 'GITHUB_TOKEN'
    PowerShellGalleryCredential = $galleryKeyPath
}
