[CmdletBinding()]
param(
    [string] $ModulePath = $env:POWERFORGE_MODULE_PATH,
    [string] $TestRoot = $env:POWERFORGE_TEST_ROOT
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $ModulePath 'PSEventViewer.psd1'
$databasePath = Join-Path $TestRoot 'events.db'
$fixturePath = Join-Path $PSScriptRoot '../Tests/Logs/NamedFilterExamples.evtx'

$Error.Clear()
Import-Module $manifestPath -Force -ErrorAction Stop
[array] $events = Get-EVXEvent -Path $fixturePath -Oldest -MaxEvents 3 -ErrorAction Stop
[array] $writeOutput = Show-EVXEvent -Path $fixturePath -Oldest -MaxEvents 3 -StorePath $databasePath -PassThru -ErrorAction Stop
$writtenReport = $writeOutput | Where-Object { $_.PSObject.Properties['Rows'] } | Select-Object -First 1
[array] $readOutput = Show-EVXEvent -FromStore $databasePath -MaxEvents 3 -PassThru -ErrorAction Stop
$readReport = $readOutput | Where-Object { $_.PSObject.Properties['Rows'] } | Select-Object -First 1

if ($Error.Count -ne 0) {
    throw "The module runtime probe emitted errors: $($Error -join ' | ')"
}
if ($events.Count -ne 3 -or @($writtenReport.Rows).Count -ne 3 -or @($readReport.Rows).Count -ne 3) {
    throw 'The module did not query, store, and reload exactly three events.'
}
if ($writtenReport.Rows[0].EventId -ne 7040 -or $writtenReport.Rows[0].RecordId -ne 37 -or
    $writtenReport.Rows[0].Values['param1'] -ne 'SmsRouter' -or
    @(Compare-Object -ReferenceObject @($events.RecordId) -DifferenceObject @($writtenReport.Rows.RecordId)).Count -ne 0) {
    throw 'The module did not query the expected retained fixture identities and payload.'
}
foreach ($writtenRow in $writtenReport.Rows) {
    [array] $matches = @($readReport.Rows | Where-Object { $_.RecordId -eq $writtenRow.RecordId })
    if ($matches.Count -ne 1 -or $matches[0].EventId -ne $writtenRow.EventId -or
        $matches[0].SourceComputer -cne $writtenRow.SourceComputer -or
        $matches[0].SourceLog -cne $writtenRow.SourceLog -or
        $matches[0].Values['param1'] -cne $writtenRow.Values['param1']) {
        throw 'The module did not preserve each retained fixture identity and payload through storage.'
    }
}

[pscustomobject] @{
    PSVersion = $PSVersionTable.PSVersion.ToString()
    QueryCount = $events.Count
    WrittenRows = @($writtenReport.Rows).Count
    ReadRows = @($readReport.Rows).Count
    DatabaseBytes = (Get-Item -LiteralPath $databasePath).Length
} | ConvertTo-Json -Compress
