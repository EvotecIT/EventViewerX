param(
    [string] $ModulePath,

    [switch] $InternalHostProbe,

    [string] $HostName,

    [string] $DatabasePath
)

$ErrorActionPreference = 'Stop'

if (-not $ModulePath) {
    $ModulePath = Join-Path $PSScriptRoot '..\Artefacts\Unpacked\Modules\PSEventViewer'
}
$ModulePath = [System.IO.Path]::GetFullPath($ModulePath)
$manifestPath = Join-Path $ModulePath 'PSEventViewer.psd1'

if ($InternalHostProbe) {
    if (-not $HostName) {
        throw 'HostName is required for an internal runtime probe.'
    }
    if (-not $DatabasePath) {
        throw 'DatabasePath is required for an internal runtime probe.'
    }

    $Error.Clear()
    Import-Module $manifestPath -Force -ErrorAction Stop
    $importErrors = $Error.Count

    $Error.Clear()
    [array] $events = Get-EVXEvent -LogName System -MaxEvents 3 -ErrorAction Stop
    $queryErrors = $Error.Count

    $Error.Clear()
    [array] $writeOutput = Show-EVXEvent -LogName System -MaxEvents 3 `
        -StorePath $DatabasePath -PassThru -ErrorAction Stop
    $writeErrors = $Error.Count
    $writtenReport = $writeOutput | Where-Object { $_.PSObject.Properties['Rows'] } |
        Select-Object -First 1

    $Error.Clear()
    [array] $readOutput = Show-EVXEvent -FromStore $DatabasePath -MaxEvents 3 `
        -PassThru -ErrorAction Stop
    $readErrors = $Error.Count
    $readReport = $readOutput | Where-Object { $_.PSObject.Properties['Rows'] } |
        Select-Object -First 1

    $result = [pscustomobject] @{
        Host          = $HostName
        PSVersion     = $PSVersionTable.PSVersion.ToString()
        ImportErrors  = $importErrors
        QueryCount    = $events.Count
        QueryErrors   = $queryErrors
        WrittenRows   = @($writtenReport.Rows).Count
        WriteErrors   = $writeErrors
        ReadRows      = @($readReport.Rows).Count
        ReadErrors    = $readErrors
        DatabaseBytes = (Get-Item -LiteralPath $DatabasePath).Length
    }

    if ($result.ImportErrors -ne 0 -or
        $result.QueryErrors -ne 0 -or
        $result.WriteErrors -ne 0 -or
        $result.ReadErrors -ne 0) {
        throw "$HostName added one or more non-terminating errors during the runtime probe."
    }
    if ($result.QueryCount -ne 3 -or $result.WrittenRows -ne 3 -or $result.ReadRows -ne 3) {
        throw "$HostName did not query, store, and reload exactly three events."
    }

    $result | ConvertTo-Json -Compress
    exit 0
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "The unpacked PSEventViewer manifest was not found at '$manifestPath'."
}

$standardPath = Join-Path $ModulePath 'Lib\Standard'
$requiredNativeAssets = @(
    'runtimes\win-x64\native\e_sqlite3.dll'
    'runtimes\win-x86\native\e_sqlite3.dll'
    'runtimes\win-arm64\native\e_sqlite3.dll'
)
foreach ($relativePath in $requiredNativeAssets) {
    $assetPath = Join-Path $standardPath $relativePath
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "The packaged PowerShell 7 payload is missing '$relativePath'."
    }
}

$powerShell7 = Get-Command pwsh -CommandType Application -ErrorAction Stop |
    Select-Object -First 1
$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf)) {
    throw "Windows PowerShell 5.1 was not found at '$windowsPowerShell'."
}

$hosts = @(
    [pscustomobject] @{ Name = 'PowerShell7'; Path = $powerShell7.Source }
    [pscustomobject] @{ Name = 'PowerShell51'; Path = $windowsPowerShell }
)

foreach ($hostInfo in $hosts) {
    $databasePath = Join-Path ([System.IO.Path]::GetTempPath()) `
        ('evx-runtime-' + $hostInfo.Name + '-' + [guid]::NewGuid().ToString('N') + '.db')
    try {
        [array] $output = & $hostInfo.Path -NoLogo -NoProfile -NonInteractive `
            -File $PSCommandPath -InternalHostProbe -ModulePath $ModulePath `
            -HostName $hostInfo.Name -DatabasePath $databasePath 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "$($hostInfo.Name) runtime probe failed:`n$($output -join [Environment]::NewLine)"
        }
        $output
    } finally {
        if (Test-Path -LiteralPath $databasePath) {
            Remove-Item -LiteralPath $databasePath -Force
        }
    }
}
