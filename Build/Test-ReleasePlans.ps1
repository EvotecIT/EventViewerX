[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$buildAllPath = Join-Path $PSScriptRoot 'Build-All.ps1'
$buildCliPath = Join-Path $PSScriptRoot 'Build-Cli.ps1'
$buildModulePath = Join-Path $PSScriptRoot 'Build-Module.ps1'

$modulePlan = & $buildModulePath -RunMode Plan
if ($null -eq $modulePlan -or -not $modulePlan.Success) {
    throw 'The module/package release plan did not succeed.'
}
if ($null -eq $modulePlan.ModulePlan -or
    $modulePlan.ModulePlan.IncludesProjectPackages -ne $true) {
    throw 'The module/package release plan must include EventViewerX NuGet packages.'
}
if (@($modulePlan.ModuleAssets | Where-Object { $_ -like '*EventViewerX.Cli.*.nupkg' }).Count -ne 0) {
    throw 'The module/package release plan must exclude the EventViewerX.Cli .NET tool package.'
}
if ($null -ne $modulePlan.DotNetToolPlan -or
    $modulePlan.ModulePlan.UnifiedGitHubRelease -eq $true) {
    throw 'The module/package release plan must exclude standalone CLI and unified GitHub publication.'
}

$fullPlan = & $buildAllPath -RunMode Plan
if ($null -eq $fullPlan -or -not $fullPlan.Success) {
    throw 'The full release plan did not succeed.'
}
if ($null -eq $fullPlan.DotNetToolPlan -or
    @($fullPlan.DotNetToolPlan.Targets.Combinations).Count -ne 12) {
    throw 'The full release plan must include all 12 standalone CLI artifacts.'
}
if ($fullPlan.ModulePlan.UnifiedGitHubRelease -ne $true) {
    throw 'The full release plan must retain unified GitHub publication.'
}

$cliPlan = & $buildCliPath -RunMode Plan
if ($null -eq $cliPlan -or -not $cliPlan.Success) {
    throw 'The CLI-only release plan did not succeed.'
}
if ($null -ne $cliPlan.ModulePlan -or $null -eq $cliPlan.DotNetToolPlan) {
    throw 'The CLI-only release plan must exclude the module lane and include the CLI tool lane.'
}
if (@($cliPlan.DotNetToolPlan.Targets.Combinations).Count -ne 12 -or
    $null -ne $cliPlan.UnifiedGitHubRelease) {
    throw 'The CLI-only release plan must contain exactly 12 CLI artifacts without unified publication.'
}

[pscustomobject] @{
    ModulePackages = $modulePlan.ModulePlan.IncludesProjectPackages
    ModuleCliAssets = 0
    FullCliAssets = @($fullPlan.DotNetToolPlan.Targets.Combinations).Count
    FullUnifiedGitHubRelease = $fullPlan.ModulePlan.UnifiedGitHubRelease
    CliOnlyAssets = @($cliPlan.DotNetToolPlan.Targets.Combinations).Count
}
