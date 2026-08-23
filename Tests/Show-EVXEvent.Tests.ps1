Describe 'Show-EVXEvent' {
    It 'keeps Type, generic LogName, Definition, and pipeline input mutually exclusive' {
        $Command = Get-Command Show-EVXEvent
        $Command.DefaultParameterSet | Should -Be 'Input'
        $Command.ParameterSets.Name | Sort-Object |
            Should -Be (@('Type', 'Log', 'Path', 'Definition', 'Input', 'Store') | Sort-Object)
        ($Command.ParameterSets | Where-Object Name -EQ 'Type').Parameters.Name |
            Should -Not -Contain 'LogName'
        ($Command.ParameterSets | Where-Object Name -EQ 'Log').Parameters.Name |
            Should -Contain 'LogName'
        ($Command.ParameterSets | Where-Object Name -EQ 'Path').Parameters.Name |
            Should -Not -Contain 'MachineName'
        ($Command.ParameterSets | Where-Object Name -EQ 'Type').Parameters.Name |
            Should -Contain 'Path'
        ($Command.ParameterSets | Where-Object Name -EQ 'Definition').Parameters.Name |
            Should -Contain 'Path'
        ($Command.ParameterSets | Where-Object Name -EQ 'Definition').Parameters.Name |
            Should -Contain 'MaxEventsScanned'
        ($Command.ParameterSets | Where-Object Name -EQ 'Store').Parameters.Name |
            Should -Contain 'SummaryPeriod'
    }

    It 'uses Path alone for a generic offline report' {
        $FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        $Report = Show-EVXEvent -Path $FixturePath -MaxEvents 2 -PassThru

        $Report.Rows.Count | Should -Be 2
        $Report.Sections.Count | Should -Be 1
        $Report.Sections[0].Kind.ToString() | Should -Be 'Generic'
        $Report.Sections[0].Columns.Name | Should -Contain 'EventId'
    }

    It 'rejects context authorization without a persistent context store' {
        $FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        {
            Show-EVXEvent `
                -Type OSStartup `
                -Path $FixturePath `
                -ContextAuthorization 'authorized-partition' `
                -PassThru `
                -ErrorAction Stop
        } | Should -Throw '*ContextAuthorization requires ContextStorePath*'
    }

    It 'renders HTML, Excel, email, and the report from one supplied snapshot' {
        $Event = Get-EVXEvent -LogName System -MaxEvents 1 -ReadMode StructuredDataAndMessage |
            Select-Object -First 1
        if (-not $Event) {
            Set-ItResult -Skipped -Because 'The System event log contained no readable events.'
            return
        }
        $HtmlPath = Join-Path $TestDrive 'event-report.html'
        $ExcelPath = Join-Path $TestDrive 'event-report.xlsx'
        $CsvPath = Join-Path $TestDrive 'event-report.csv'

        $Result = @($Event | Show-EVXEvent `
                -Title 'System snapshot' `
                -HtmlPath $HtmlPath `
                -DrawerPlacement Top `
                -ExcelPath $ExcelPath `
                -CsvPath $CsvPath `
                -EmailPackage `
                -PassThru)

        Test-Path -LiteralPath $HtmlPath | Should -BeTrue
        Test-Path -LiteralPath $ExcelPath | Should -BeTrue
        Test-Path -LiteralPath $CsvPath | Should -BeTrue
        (Get-Content -LiteralPath $HtmlPath -Raw) | Should -Match 'System snapshot'
        (Get-Content -LiteralPath $HtmlPath -Raw) | Should -Match 'data-hfx-monitoring-record-drawer-placement="top"'
        $Result.Count | Should -Be 5
        @($Result | Where-Object { $_ -is [EventViewerX.Reporting.EventEmailPackage] }).Count |
            Should -Be 1
        $Report = $Result | Where-Object { $_ -is [EventViewerX.Reporting.EventReport] }
        $Report.Rows.Count | Should -Be 1
    }

    It 'combines a custom definition with an offline path without a second query mode' {
        $DefinitionPath = Join-Path $TestDrive 'service-change.json'
        @{
            Name = 'ServiceStartTypeChange'
            DisplayName = 'Service start type changes'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        $FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        $Report = Show-EVXEvent -Definition $DefinitionPath -Path $FixturePath -MaxEvents 2 -PassThru

        $Report.Rows.Count | Should -Be 2
        @($Report.Rows.Type | Sort-Object -Unique) | Should -Be @('ServiceStartTypeChange')
        $Report.Coverage[0].MachineName | Should -Be 'Offline'
    }

    It 'applies the same typed Where contract before rendering a custom report' {
        $DefinitionPath = Join-Path $TestDrive 'service-change-filtered.json'
        @{
            Name = 'ServiceStartTypeChange'
            DisplayName = 'Service start type changes'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        $FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        $Report = Show-EVXEvent `
            -Definition $DefinitionPath `
            -Path $FixturePath `
            -Where { $_.ServiceName -eq 'BITS' } `
            -PassThru

        $Report.Rows.Count | Should -BeGreaterThan 0
        @($Report.Sections[0].Rows | ForEach-Object { $_.Values['ServiceName'] } |
                Sort-Object -Unique) | Should -Be 'BITS'
    }

    It 'persists, filters, and summarizes typed rows without adding storage cmdlets' {
        $DefinitionPath = Join-Path $TestDrive 'stored-service-change.json'
        $StorePath = Join-Path $TestDrive 'events.db'
        @{
            Name = 'ServiceStartTypeChange'
            DisplayName = 'Service start type changes'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceName'
                    Aliases = @('ProviderName')
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        $FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        $StoredOutput = @(Show-EVXEvent `
                -Definition $DefinitionPath `
                -Path $FixturePath `
                -MaxEvents 4 `
                -StorePath $StorePath `
                -PassThru)
        $StoredReport = $StoredOutput | Where-Object { $_ -is [EventViewerX.Reporting.EventReport] }
        $Filtered = Show-EVXEvent `
            -FromStore $StorePath `
            -Definition ServiceStartTypeChange `
            -Where { $_.ServiceName -eq 'BITS' } `
            -PassThru
        $FilteredThroughAlias = Show-EVXEvent `
            -FromStore $StorePath `
            -Definition $DefinitionPath `
            -Where { $_.ProviderName -eq 'BITS' } `
            -PassThru
        $FilteredThroughStoredAlias = Show-EVXEvent `
            -FromStore $StorePath `
            -Definition ServiceStartTypeChange `
            -Where { $_.ProviderName -eq 'BITS' } `
            -PassThru
        $Summary = Show-EVXEvent `
            -FromStore $StorePath `
            -SummaryPeriod Day `
            -PassThru

        Test-Path -LiteralPath $StorePath | Should -BeTrue
        $StoredReport.Rows.Count | Should -Be 4
        $Filtered.Rows.Count | Should -BeGreaterThan 0
        @($Filtered.Rows | ForEach-Object { $_.Values['ServiceName'] } | Sort-Object -Unique) |
            Should -Be @('BITS')
        $Filtered.Sections[0].Columns.Name | Should -Be @('ServiceName')
        $FilteredThroughAlias.Rows.Count | Should -Be $Filtered.Rows.Count
        @($FilteredThroughAlias.Rows | ForEach-Object { $_.Values['ServiceName'] } | Sort-Object -Unique) |
            Should -Be @('BITS')
        $FilteredThroughStoredAlias.Rows.Count | Should -Be $Filtered.Rows.Count
        @($FilteredThroughStoredAlias.Rows | ForEach-Object { $_.Values['ServiceName'] } | Sort-Object -Unique) |
            Should -Be @('BITS')
        $Summary.Sections[0].Name | Should -Be 'EventStoreSummary'
        $Summary.Sections[0].Columns.Name | Should -Contain 'Count'
    }

    It 'renders empty stored custom CSV with the supplied definition schema' {
        $DefinitionPath = Join-Path $TestDrive 'empty-stored-definition.json'
        $StorePath = Join-Path $TestDrive 'empty-stored-events.db'
        $CsvPath = Join-Path $TestDrive 'empty-stored-events.csv'
        @{
            Name = 'EmptyStoredAudit'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(
                @{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param4'
                }
                @{
                    Name = 'ProjectedId'
                    ValueKind = 'Int32'
                    Source = 'Metadata'
                    SourceName = 'EventId'
                }
            )
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $Report = Show-EVXEvent `
            -FromStore $StorePath `
            -Definition $DefinitionPath `
            -CsvPath $CsvPath `
            -PassThru

        $Report.Rows.Count | Should -Be 0
        $Report.Sections[0].Columns.Name | Should -Be @('ServiceName', 'ProjectedId')
        Test-Path -LiteralPath $CsvPath | Should -BeTrue
        (Get-Content -LiteralPath $CsvPath -TotalCount 1) | Should -Match 'Service Name'
        (Get-Content -LiteralPath $CsvPath -TotalCount 1) | Should -Match 'Projected ID'
    }

    It 'normalizes built-in typed predicates before opening stored history' {
        $StorePath = Join-Path $TestDrive 'not-opened.db'

        {
            Show-EVXEvent `
                -FromStore $StorePath `
                -Type ADUserLogonFailed `
                -Where { $_.Who -gt 'M' } `
                -PassThru
        } | Should -Throw "*Operator 'GreaterThan' is not supported by field 'Who'*"
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'uses stored schema metadata when parsing stable-name script filters' {
        $StorePath = Join-Path $TestDrive 'stored-collection-schema.db'
        $Column = [EventViewerX.Reporting.EventReportColumnSchema]::new()
        $Column.Name = 'Privileges'
        $Column.DisplayName = 'Privileges'
        $Column.ValueTypeName = [EventViewerX.Reporting.EventReportColumnSchema]::GetStableTypeName([string[]])
        $Schema = [EventViewerX.Reporting.EventReportSectionSchema]::new()
        $Schema.Name = 'Audit'
        $Schema.DisplayName = 'Audit'
        $Schema.Kind = [EventViewerX.Reporting.EventReportSectionKind]::Custom
        $Columns = [Collections.Generic.List[EventViewerX.Reporting.EventReportColumnSchema]]::new()
        $Columns.Add($Column)
        $Schema.Columns = $Columns
        $Values = [Collections.Generic.Dictionary[string, object]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        $Values['Privileges'] = [string[]] @('SeDebugPrivilege', 'SeBackupPrivilege')
        $Row = [EventViewerX.Reporting.EventReportRow]::new()
        $Row.Type = 'Audit'
        $Row.TimeCreated = [datetime]::SpecifyKind([datetime]'2026-08-01T01:00:00', [DateTimeKind]::Utc)
        $Row.EventId = 4672
        $Row.RecordId = 42
        $Row.Provider = 'Microsoft-Windows-Security-Auditing'
        $Row.SourceLog = 'Security'
        $Row.ContainerLog = 'Security'
        $Row.SourceComputer = 'AD0'
        $Row.CollectorComputer = 'AD0'
        $Row.Values = $Values
        $Report = [EventViewerX.Reporting.EventReportEngine]::CreateStored(
            [EventViewerX.Reporting.EventReportRow[]] @($Row),
            [EventViewerX.Reporting.EventReportSectionSchema[]] @($Schema))
        $Store = [EventViewerX.Storage.EventStore]::new($StorePath)
        $null = $Store.WriteAsync($Report).GetAwaiter().GetResult()

        {
            Show-EVXEvent `
                -FromStore $StorePath `
                -Definition Audit `
                -Where { $_.Privileges -in @('SeDebugPrivilege') } `
                -PassThru
        } | Should -Throw '*Field-left -in/-notin treats collection field*'
        {
            Show-EVXEvent `
                -FromStore $StorePath `
                -Where { 'SeDebugPrivilege' -eq $_.Privileges } `
                -PassThru
        } | Should -Throw '*Field-right -eq/-ne treats collection field*'
        $Matched = Show-EVXEvent `
            -FromStore $StorePath `
            -Definition Audit `
            -Where { 'SeDebugPrivilege' -in $_.Privileges } `
            -PassThru

        $Matched.Rows.Count | Should -Be 1

        $SecondSchema = [EventViewerX.Reporting.EventReportSectionSchema]::new()
        $SecondSchema.Name = 'OtherAudit'
        $SecondSchema.DisplayName = 'Other audit'
        $SecondSchema.Kind = [EventViewerX.Reporting.EventReportSectionKind]::Custom
        $SecondSchema.Columns = $Columns
        $SecondRow = [EventViewerX.Reporting.EventReportRow]::new()
        $SecondRow.Type = 'OtherAudit'
        $SecondRow.TimeCreated = [datetime]::SpecifyKind([datetime]'2026-08-01T02:00:00', [DateTimeKind]::Utc)
        $SecondRow.EventId = 4672
        $SecondRow.RecordId = 43
        $SecondRow.Provider = 'Microsoft-Windows-Security-Auditing'
        $SecondRow.SourceLog = 'Security'
        $SecondRow.ContainerLog = 'Security'
        $SecondRow.SourceComputer = 'AD0'
        $SecondRow.CollectorComputer = 'AD0'
        $SecondRow.Values = $Values
        $SecondReport = [EventViewerX.Reporting.EventReportEngine]::CreateStored(
            [EventViewerX.Reporting.EventReportRow[]] @($SecondRow),
            [EventViewerX.Reporting.EventReportSectionSchema[]] @($SecondSchema))
        $null = $Store.WriteAsync($SecondReport).GetAwaiter().GetResult()

        {
            Show-EVXEvent `
                -FromStore $StorePath `
                -Where { $_.Privileges -contains 'SeDebugPrivilege' } `
                -PassThru
        } | Should -Throw '*current stored selection exposes 2 schemas*'

        $TypedPredicate = [EventViewerX.EventPredicate]::Compare(
            'EventId',
            [EventViewerX.EventPredicateOperator]::Equal,
            4672)
        $UnscopedTyped = Show-EVXEvent `
            -FromStore $StorePath `
            -Where $TypedPredicate `
            -PassThru
        $UnscopedTyped.Rows.Count | Should -Be 2
    }

    It 'rejects simultaneous built-in and custom stored selectors before opening history' {
        $StorePath = Join-Path $TestDrive 'not-opened-mixed-selectors.db'

        {
            Show-EVXEvent `
                -FromStore $StorePath `
                -Type ADUserLogonFailed `
                -Definition ServiceStartTypeChange `
                -PassThru
        } | Should -Throw '*Type and Definition are mutually exclusive*'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'rejects summary persistence before reading or rendering output' {
        $FromStore = Join-Path $TestDrive 'not-opened-summary.db'
        $StorePath = Join-Path $TestDrive 'not-created-summary-copy.db'
        $HtmlPath = Join-Path $TestDrive 'not-created-summary.html'

        {
            Show-EVXEvent `
                -FromStore $FromStore `
                -SummaryPeriod Day `
                -StorePath $StorePath `
                -HtmlPath $HtmlPath
        } | Should -Throw '*SummaryPeriod and StorePath cannot be combined*'

        Test-Path -LiteralPath $FromStore | Should -BeFalse
        Test-Path -LiteralPath $StorePath | Should -BeFalse
        Test-Path -LiteralPath $HtmlPath | Should -BeFalse
    }
}
