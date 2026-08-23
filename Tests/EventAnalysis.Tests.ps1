Describe 'Event analysis PowerShell surface' {
    It 'accepts mixed string and hashtable measures through the pipeline' {
        $Schema = [EventViewerX.Reporting.EventReportSectionSchema]::new()
        $Schema.Name = 'Generic'
        $Schema.DisplayName = 'Events'
        $Schema.Kind = [EventViewerX.Reporting.EventReportSectionKind]::Generic
        $Rows = foreach ($Index in 1..2) {
            $Row = [EventViewerX.Reporting.EventReportRow]::new()
            $Row.Type = 'Generic'
            $Row.TimeCreated = [datetime]::SpecifyKind(
                [datetime] "2026-08-01T0$($Index):00:00",
                [DateTimeKind]::Utc)
            $Row.EventId = 4624
            $Row.RecordId = $Index
            $Row.Provider = 'Microsoft-Windows-Security-Auditing'
            $Row.SourceLog = 'Security'
            $Row.ContainerLog = 'Security'
            $Row.SourceComputer = "AD$Index"
            $Row.CollectorComputer = 'WEC1'
            $Row
        }
        $Report = [EventViewerX.Reporting.EventReportEngine]::CreateStored(
            [EventViewerX.Reporting.EventReportRow[]] $Rows,
            [EventViewerX.Reporting.EventReportSectionSchema[]] @($Schema))

        $Result = $Report | Measure-EVXEvent `
            -GroupBy Type `
            -Measure 'Count::Events', @{
                Operation  = 'DistinctCount'
                Field      = 'SourceComputer'
                OutputName = 'Sources'
            }

        $Result.IsComplete | Should -BeTrue
        $Result.InputRows | Should -Be 2
        $Result.Rows.Count | Should -Be 1
        $Result.Rows[0].Measures['Events'] | Should -Be 2
        $Result.Rows[0].Measures['Sources'] | Should -Be 2
    }

    It 'keeps occurrence grouping non-destructive for supplied events' {
        $Event = Get-EVXEvent `
            -Path (Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx') `
            -Oldest `
            -MaxEvents 1

        @($Event).Count | Should -Be 1

        $Result = $Event | Show-EVXEvent -DuplicateMode Transport -PassThru

        $Result | Should -BeOfType ([EventViewerX.Reporting.EventOccurrenceResult])
        $Result.Groups.Count | Should -Be 1
        $Result.Groups[0].Observations.Count | Should -Be 1
        $Result.Groups[0].Representative.RecordId | Should -Be $Event.RecordId
    }

    It 'accepts report rows directly and keeps pipeline completeness unknown' {
        $Row = [EventViewerX.Reporting.EventReportRow]::new()
        $Row.Type = 'Generic'
        $Row.TimeCreated = [datetime]::UtcNow
        $Row.Provider = 'Provider'
        $Row.SourceLog = 'Security'
        $Row.SourceComputer = 'DC1'

        $Result = $Row | Measure-EVXEvent -GroupBy ProviderName

        $Result.InputCompleteness | Should -Be ([EventViewerX.Reporting.EventAggregationInputCompleteness]::Unknown)
        $Result.IsComplete | Should -BeFalse
        $Result.Rows.Count | Should -Be 1
        $Result.Rows[0].Group['ProviderName'] | Should -Be 'Provider'
    }
}
