[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

$buildModulePath = Join-Path $RepositoryRoot 'Build\Build-Module.ps1'
$tokens = $null
$parseErrors = $null
$buildModuleAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $buildModulePath,
    [ref] $tokens,
    [ref] $parseErrors)
if (@($parseErrors).Count -ne 0) {
    throw 'Build\Build-Module.ps1 must parse before its release defaults are validated.'
}
$runModeParameter = $buildModuleAst.ParamBlock.Parameters | Where-Object {
    $_.Name.VariablePath.UserPath -eq 'RunMode'
}
if ($null -eq $runModeParameter -or
    [string] $runModeParameter.DefaultValue.SafeGetValue() -cne 'Build') {
    throw 'Build\Build-Module.ps1 must default to Build; publication requires an explicit RunMode.'
}

$release = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Build\release.json') -Raw |
    ConvertFrom-Json
if ([string] $release.GitHub.TokenEnvName -ne 'GITHUB_TOKEN' -or
    -not [string]::IsNullOrWhiteSpace([string] $release.GitHub.TokenFilePath)) {
    throw 'The unified GitHub release must use GITHUB_TOKEN without a machine-specific token path.'
}
if ($null -eq $release.Tools -or $release.GitHub.Publish -ne $true) {
    throw 'The full release must include CLI tools and the unified GitHub release.'
}
$cliBuildScript = Get-Content -LiteralPath `
    (Join-Path $RepositoryRoot 'Build\Build-Cli.ps1') -Raw
if ($cliBuildScript -notmatch 'StageRoot\s*=\s*Join-Path\s+\$repositoryRoot\s+''Artefacts\\UploadReady\\Cli''') {
    throw 'The CLI-only wrapper must use its dedicated Artefacts\UploadReady\Cli staging root.'
}
if ($null -ne $release.Module.PSObject.Properties['ModuleVersion']) {
    throw 'The full release profile must inherit its module version from Build/module.json.'
}
if (@($release.Module.ArtifactPaths | Where-Object { $_ -like '*EventViewerX.Cli.*.nupkg' }).Count -ne 0) {
    throw 'The full release must not publish the deferred EventViewerX.Cli .NET tool package.'
}

$moduleRelease = Get-Content -LiteralPath `
    (Join-Path $RepositoryRoot 'Build\release.module.json') -Raw |
    ConvertFrom-Json
if ($null -ne $moduleRelease.Tools -or $null -ne $moduleRelease.GitHub) {
    throw 'The module release must not include CLI tools or a unified GitHub release.'
}
if ($moduleRelease.Module.IncludesPackages -ne $true -or
    [string] $moduleRelease.Module.ConfigPath -ne 'Build/module.json') {
    throw 'The module release must include the canonical EventViewerX package configuration.'
}
if ($null -ne $moduleRelease.Module.PSObject.Properties['ModuleVersion']) {
    throw 'The module release profile must inherit its module version from Build/module.json.'
}
if (@($moduleRelease.Module.ArtifactPaths | Where-Object { $_ -like '*EventViewerX.Cli.*.nupkg' }).Count -ne 0) {
    throw 'The module release must not publish the deferred EventViewerX.Cli .NET tool package.'
}
[array] $fullModuleAssets = @($release.Module.ArtifactPaths | Sort-Object)
[array] $moduleOnlyAssets = @($moduleRelease.Module.ArtifactPaths | Sort-Object)
if (($fullModuleAssets -join "`n") -cne ($moduleOnlyAssets -join "`n")) {
    throw 'The full and module-only release profiles must use the same package/module artifact set.'
}
[array] $moduleReleaseValidations = @($moduleRelease.Validation.AfterStaging)
if ($moduleReleaseValidations.Count -ne 1 -or
    [string] $moduleReleaseValidations[0].ConfigPath -ne 'release-validation.json') {
    throw 'The module release must validate its exact staged package and module artifacts.'
}

$projectBuild = Get-Content -LiteralPath `
    (Join-Path $RepositoryRoot 'Sources\Build\project.build.json') -Raw |
    ConvertFrom-Json
if ($null -ne $projectBuild.ExpectedVersionMap.PSObject.Properties['EventViewerX.Cli']) {
    throw 'The EventViewerX.Cli .NET tool package must remain outside the library/module release.'
}
[array] $libraryVersions = @($projectBuild.ExpectedVersionMap.PSObject.Properties.Value |
    Select-Object -Unique)
if ($libraryVersions.Count -ne 1 -or [string] $libraryVersions[0] -ne '4.0.X') {
    throw 'All EventViewerX library packages must use the configured 4.0.X version track.'
}
$nuGetKeyPath = [string] $projectBuild.PublishApiKeyFilePath
if ([string] $projectBuild.PublishApiKeyEnvName -ne 'NUGET_API_KEY' -or
    [System.IO.Path]::IsPathRooted($nuGetKeyPath) -or
    $nuGetKeyPath -ne '../../.secrets/NugetOrgEvotec.txt') {
    throw 'NuGet publication must use the ignored repo-local key path with NUGET_API_KEY as fallback.'
}
if ([string] $projectBuild.GitHubAccessTokenEnvName -ne 'GITHUB_TOKEN' -or
    -not [string]::IsNullOrWhiteSpace([string] $projectBuild.GitHubAccessTokenFilePath)) {
    throw 'Project GitHub authentication must use GITHUB_TOKEN without a machine-specific token path.'
}

$moduleBuild = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Build\module.json') -Raw |
    ConvertFrom-Json
if ([string] $moduleBuild.Build.Version -ne '4.0.X') {
    throw 'Build/module.json must remain the module version source with the 4.0.X track.'
}
[array] $excludedDirectories = @($moduleBuild.Build.ExcludeDirectories)
foreach ($requiredExclusion in @(
        '.codex-artifacts'
        '.dotnet'
        '.secrets'
        'Artifacts'
        'BenchmarkDotNet.Artifacts'
    )) {
    if ($excludedDirectories -cnotcontains $requiredExclusion) {
        throw "Module packaging must exclude the repo-local '$requiredExclusion' directory."
    }
}
[array] $gallerySegments = @($moduleBuild.Segments | Where-Object {
    $_.Type -eq 'GalleryNuget' -and $_.Configuration.Enabled -eq $true
})
if ($gallerySegments.Count -ne 1) {
    throw "Expected one enabled PowerShell Gallery publication segment, found $($gallerySegments.Count)."
}
$galleryKeyPath = [string] $gallerySegments[0].Configuration.ApiKeyFilePath
if ([System.IO.Path]::IsPathRooted($galleryKeyPath) -or
    $galleryKeyPath -ne '.secrets/PowerShellGalleryAPI.txt') {
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
    NuGetCredential = $nuGetKeyPath
    GitHubCredential = 'GITHUB_TOKEN'
    PowerShellGalleryCredential = $galleryKeyPath
    LocalRuntimeAndOutputExcluded = $true
    CliStagingIsolated = $true
    FullReleaseIncludesCli = $true
    ModuleReleaseIncludesCli = $false
    CliNuGetPackageIncluded = $false
}
