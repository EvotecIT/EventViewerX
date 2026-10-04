Describe 'Show-EVXEvent privacy exports' {
    It 'returns a minimized snapshot while preserving original stored evidence' {
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $Store = Join-Path $TestDrive 'originals.db'
        $Policy = [EventViewerX.Reporting.EventReportPrivacyOptions]::new()
        $Output = @(Show-EVXEvent -Path $Fixture -Oldest -MaxEvents 1 -Privacy $Policy -StorePath $Store -PassThru)
        $Export = @($Output | Where-Object { $_ -is [EventViewerX.Reporting.EventReport] })[0]
        $Export.Rows[0].Message | Should -BeNullOrEmpty
        $Export.Rows[0].Values.Count | Should -Be 0
        $Export.Rows[0].SourceComputer | Should -BeNullOrEmpty
        $Original = Show-EVXEvent -FromStore $Store -PassThru
        $Original.Rows[0].Values['param1'] | Should -Be 'SmsRouter'
        $Original.Rows[0].SourceComputer | Should -Not -BeNullOrEmpty
    }

    It 'renders a detached snapshot through the input pipeline without querying again' {
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $Original = Show-EVXEvent -Path $Fixture -Oldest -MaxEvents 1 -PassThru
        $Relabeled = $Original | Show-EVXEvent -Title 'Snapshot title' -PassThru
        $Relabeled.Title | Should -Be 'Snapshot title'
        $Relabeled.EventsScanned | Should -Be $Original.EventsScanned
        $Relabeled.QueryDuration | Should -Be $Original.QueryDuration
        $Relabeled.GeneratedAt | Should -Be $Original.GeneratedAt
        $Relabeled.CompletenessDiagnostic | Should -Be $Original.CompletenessDiagnostic
        $Policy = [EventViewerX.Reporting.EventReportPrivacyOptions]::new()
        $Export = $Original | Show-EVXEvent -Privacy $Policy -PassThru
        $Renamed = $Export | Show-EVXEvent -Title 'Shared export' -PassThru
        $Renamed.Title | Should -Be 'Shared export'
        $Renamed.Rows[0].Values.Count | Should -Be 0
        $Export.Rows.Count | Should -Be 1
        $Export.Rows[0].Message | Should -BeNullOrEmpty
        $Original.Rows[0].Values['param1'] | Should -Be 'SmsRouter'
    }
}
