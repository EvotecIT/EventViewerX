Import-Module PSEventViewer -Force

$definitionPath = Join-Path $PSScriptRoot 'CustomEvent.definition.json'

Get-EVXEvent `
    -Definition $definitionPath `
    -TimePeriod Last24Hours `
    -MaxEvents 100 |
    Select-Object TimeCreated, TypeName, ServiceName, OldStartType,
        NewStartType, Computer

Show-EVXEvent `
    -Definition $definitionPath `
    -TimePeriod Last7Days `
    -HtmlPath (Join-Path $PSScriptRoot 'ServiceChanges.html') `
    -ExcelPath (Join-Path $PSScriptRoot 'ServiceChanges.xlsx')
