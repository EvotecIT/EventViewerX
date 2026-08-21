Import-Module PSEventViewer -Force

$StorePath = Join-Path $env:ProgramData 'EventViewerX\events.db'

# Run on a schedule against the collector. A deliberate overlap prevents gaps;
# provenance-based deduplication makes repeated events harmless.
Show-EVXEvent `
    -Type ActiveDirectoryAuthentication `
    -Collector WEC01 `
    -TimePeriod Last15Minutes `
    -StorePath $StorePath

# Definition fields are discoverable and reusable across live and stored data.
$Filter = New-EVXFilter -Type ADUserLogonFailed
$Predicate = $Filter.Fields.Who.MatchesWildcard('CONTOSO\*')

Show-EVXEvent `
    -FromStore $StorePath `
    -Type ADUserLogonFailed `
    -Where $Predicate `
    -StartTime (Get-Date).AddDays(-7) `
    -HtmlPath (Join-Path $PSScriptRoot 'FailedLogons.html') `
    -ExcelPath (Join-Path $PSScriptRoot 'FailedLogons.xlsx') `
    -CsvPath (Join-Path $PSScriptRoot 'FailedLogons.csv')

# Calendar summaries are exhaustive. MaxCandidates remains available when a
# domain predicate requires a bounded managed scan.
Show-EVXEvent `
    -FromStore $StorePath `
    -Type ActiveDirectoryAuthentication `
    -StartTime (Get-Date).AddMonths(-1) `
    -SummaryPeriod Day `
    -HtmlPath (Join-Path $PSScriptRoot 'Authentication-Daily.html')
