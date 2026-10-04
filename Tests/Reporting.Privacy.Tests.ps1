Describe 'Show-EVXEvent privacy exports' {
    It 'keeps exact numeric pseudonyms stable across the supported PowerShell runtimes' {
        $Policy = [EventViewerX.Reporting.EventReportPrivacyOptions]::new()
        $Policy.PseudonymizedValueFields = [string[]] @('NumericId', 'SingleId')
        $Key = [byte[]] (0..31)
        $Row = [EventViewerX.Reporting.EventReportRow]::new()
        $Row.Type = 'Generic'
        $Row.Values = [Collections.Generic.Dictionary[string,object]]::new()
        $Row.Values.Add('NumericId', [BitConverter]::Int64BitsToDouble(4608238818662570491))
        $Row.Values.Add('SingleId', [BitConverter]::ToSingle([BitConverter]::GetBytes([int] 1067320914), 0))
        $Report = [EventViewerX.Reporting.EventReportEngine]::CreateStored(
            [EventViewerX.Reporting.EventReportRow[]] @($Row),
            [EventViewerX.Reporting.EventReportSectionSchema[]] @([EventViewerX.Reporting.EventReportSectionSchema]::CreateGeneric()))
        $Export = $Report | Show-EVXEvent -Privacy $Policy -PseudonymizationKey $Key -PassThru
        $Export.Rows[0].Values['NumericId'] | Should -Be 'hmac-sha256:7b10944b347e8aba678b27ff010390367c1a2465fc47c93530437f876477987b'
        $Export.Rows[0].Values['SingleId'] | Should -Be 'hmac-sha256:12d032669a54f752f42dad655248f59124ebcaa8a8f3848a6f4cac6fd8605fb5'
        $Row.Values['NumericId'] = [BitConverter]::Int64BitsToDouble(4608238818662570492)
        $Report = [EventViewerX.Reporting.EventReportEngine]::CreateStored(
            [EventViewerX.Reporting.EventReportRow[]] @($Row),
            [EventViewerX.Reporting.EventReportSectionSchema[]] @([EventViewerX.Reporting.EventReportSectionSchema]::CreateGeneric()))
        $Next = $Report | Show-EVXEvent -Privacy $Policy -PseudonymizationKey $Key -PassThru
        $Next.Rows[0].Values['NumericId'] | Should -Not -Be $Export.Rows[0].Values['NumericId']
    }

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
