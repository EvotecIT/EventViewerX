[CmdletBinding()]
param(
    [string] $ModulePath = $env:POWERFORGE_MODULE_PATH,
    [string] $TestRoot = $env:POWERFORGE_TEST_ROOT
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $ModulePath 'PSEventViewer.psd1'
$databasePath = Join-Path $TestRoot 'events.db'

$Error.Clear()
Import-Module $manifestPath -Force -ErrorAction Stop
[array] $events = Get-EVXEvent -LogName System -MaxEvents 3 -ErrorAction Stop
[array] $writeOutput = Show-EVXEvent -LogName System -MaxEvents 3 -StorePath $databasePath -PassThru -ErrorAction Stop
$writtenReport = $writeOutput | Where-Object { $_.PSObject.Properties['Rows'] } | Select-Object -First 1
[array] $readOutput = Show-EVXEvent -FromStore $databasePath -MaxEvents 3 -PassThru -ErrorAction Stop
$readReport = $readOutput | Where-Object { $_.PSObject.Properties['Rows'] } | Select-Object -First 1

if ($Error.Count -ne 0) {
    throw "The module runtime probe emitted errors: $($Error -join ' | ')"
}
if ($events.Count -ne 3 -or @($writtenReport.Rows).Count -ne 3 -or @($readReport.Rows).Count -ne 3) {
    throw 'The module did not query, store, and reload exactly three events.'
}

[pscustomobject] @{
    PSVersion = $PSVersionTable.PSVersion.ToString()
    QueryCount = $events.Count
    WrittenRows = @($writtenReport.Rows).Count
    ReadRows = @($readReport.Rows).Count
    DatabaseBytes = (Get-Item -LiteralPath $databasePath).Length
} | ConvertTo-Json -Compress
