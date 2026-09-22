Describe 'Stored history streaming and Kerberos impact' {
    It 'streams stored rows with the same order and exposes bounded completion' {
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $StorePath = Join-Path $TestDrive 'streamed-history.db'
        $null = Show-EVXEvent -Path $Fixture -MaxEvents 3 -StorePath $StorePath -PassThru

        $Rows = @(Get-EVXStoredEvent -Path $StorePath -Oldest)
        $Warnings = @()
        $Limited = @(Get-EVXStoredEvent -Path $StorePath -Oldest -MaxEvents 2 `
            -WarningVariable Warnings)

        $Rows.Count | Should -Be 3
        $Limited.Count | Should -Be 2
        $Limited.RecordId | Should -Be @($Rows[0].RecordId, $Rows[1].RecordId)
        $Limited[0].NormalizedValues.Count | Should -BeGreaterThan 0
        $Warnings | Should -Match 'MaxEvents 2'
    }

    It 'keeps an empty KDC window distinct from domain-wide readiness' {
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $StorePath = Join-Path $TestDrive 'non-kdc-history.db'
        $null = Show-EVXEvent -Path $Fixture -MaxEvents 2 -StorePath $StorePath -PassThru

        $Impact = Get-EVXKerberosImpact -FromStore $StorePath

        $Impact.EventsObserved | Should -Be 0
        $Impact.Groups.Count | Should -Be 0
        $Impact.SelectedWindowComplete | Should -BeTrue
        $Impact.CoverageStatement | Should -Match 'every domain controller'
    }
}
