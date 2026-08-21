$ModuleName = (Get-ChildItem $PSScriptRoot\*.psd1).BaseName
$PrimaryModule = Get-ChildItem -Path $PSScriptRoot -Filter '*.psd1' -Recurse -ErrorAction SilentlyContinue -Depth 1
if (-not $PrimaryModule) {
    throw "Path $PSScriptRoot doesn't contain PSD1 files. Failing tests."
}
if ($PrimaryModule.Count -ne 1) {
    throw 'More than one PSD1 files detected. Failing tests.'
}
$PSDInformation = Import-PowerShellDataFile -Path $PrimaryModule.FullName
$PesterMinimumVersion = [version] '5.0.0'
$PesterModule = Get-Module -ListAvailable -Name Pester | Sort-Object -Property Version -Descending | Select-Object -First 1
if (-not $PesterModule -or $PesterModule.Version -lt $PesterMinimumVersion) {
    Write-Warning "$ModuleName - Installing Pester $PesterMinimumVersion or newer from PSGallery"
    Install-Module -Name Pester -MinimumVersion $PesterMinimumVersion -Force -SkipPublisherCheck
}
Import-Module -Name Pester -MinimumVersion $PesterMinimumVersion -Force

$RequiredModules = @(
    'PSWriteColor'
    if ($PSDInformation.RequiredModules) {
        $PSDInformation.RequiredModules
    }
)
foreach ($Module in $RequiredModules) {
    if ($Module -is [System.Collections.IDictionary]) {
        $Exists = Get-Module -ListAvailable -Name $Module.ModuleName
        if (-not $Exists) {
            Write-Warning "$ModuleName - Downloading $($Module.ModuleName) from PSGallery"
            Install-Module -Name $Module.ModuleName -Force -SkipPublisherCheck
        }
    } else {
        $Exists = Get-Module -ListAvailable $Module -ErrorAction SilentlyContinue
        if (-not $Exists) {
            Install-Module -Name $Module -Force -SkipPublisherCheck
        }
    }
}

Write-Color 'ModuleName: ', $ModuleName, ' Version: ', $PSDInformation.ModuleVersion -Color Yellow, Green, Yellow, Green -LinesBefore 2
Write-Color 'PowerShell Version: ', $PSVersionTable.PSVersion -Color Yellow, Green
Write-Color 'PowerShell Edition: ', $PSVersionTable.PSEdition -Color Yellow, Green
Write-Color 'Required modules: ' -Color Yellow
foreach ($Module in $PSDInformation.RequiredModules) {
    if ($Module -is [System.Collections.IDictionary]) {
        Write-Color '   [>] ', $Module.ModuleName, ' Version: ', $Module.ModuleVersion -Color Yellow, Green, Yellow, Green
    } else {
        Write-Color '   [>] ', $Module -Color Yellow, Green
    }
}
Write-Color

Import-Module $PSScriptRoot\*.psd1 -Force
$invokePesterSplat = @{
    PassThru = $true
    Verbose  = $true
}
$invokePesterCommand = Get-Command -Name Invoke-Pester
if ($invokePesterCommand.Parameters.ContainsKey('Path')) {
    $invokePesterSplat['Path'] = Join-Path -Path $PSScriptRoot -ChildPath 'Tests'
} else {
    $invokePesterSplat['Script'] = Join-Path -Path $PSScriptRoot -ChildPath 'Tests'
}
$result = Invoke-Pester @invokePesterSplat

if ($result.FailedCount -gt 0) {
    throw "$($result.FailedCount) tests failed."
}
