[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackageRoot,
    [Parameter(Mandatory)] [string] $Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedFrameworks = @('net472', 'net8.0', 'net10.0')
$expectedGroups = @('.NETFramework4.7.2', 'net8.0', 'net10.0')
$packages = @(
    'EventViewerX'
    'EventViewerX.Detection'
    'EventViewerX.Evtx'
    'EventViewerX.Reporting'
    'EventViewerX.Storage'
)

foreach ($package in $packages) {
    $packagePath = Join-Path $PackageRoot "$package.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Package is missing: $packagePath"
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $assemblyName = "$package.dll"
        $allLibraryFrameworks = @(
            foreach ($entry in $archive.Entries) {
                if ($entry.FullName -match '^lib/([^/]+)/[^/]+$') {
                    $Matches[1]
                }
            }
        ) | Sort-Object -Unique
        $actualFrameworks = @(
            foreach ($entry in $archive.Entries) {
                if ($entry.FullName -match '^lib/([^/]+)/([^/]+)$' -and $Matches[2] -eq $assemblyName) {
                    $Matches[1]
                }
            }
        ) | Sort-Object -Unique
        $unexpected = @($allLibraryFrameworks | Where-Object { $_ -notin $expectedFrameworks })
        $missing = @($expectedFrameworks | Where-Object { $_ -notin $actualFrameworks })
        if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
            throw "$package has library targets '$($allLibraryFrameworks -join ', ')'; expected exactly '$($expectedFrameworks -join ', ')'."
        }

        $nuspecEntry = $archive.GetEntry("$package.nuspec")
        if (-not $nuspecEntry) {
            throw "$package has no nuspec."
        }
        $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }
        $dependencyGroups = @($nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']") |
            ForEach-Object { $_.GetAttribute('targetFramework') })
        $unexpectedGroups = @($dependencyGroups | Where-Object { $_ -notin $expectedGroups })
        $missingGroups = @($expectedGroups | Where-Object { $_ -notin $dependencyGroups })
        if ($unexpectedGroups.Count -gt 0 -or $missingGroups.Count -gt 0) {
            throw "$package has dependency groups '$($dependencyGroups -join ', ')'; expected exactly '$($expectedGroups -join ', ')'."
        }
        Write-Host "$package package targets: $($expectedFrameworks -join ', ')"
    } finally {
        $archive.Dispose()
    }
}
