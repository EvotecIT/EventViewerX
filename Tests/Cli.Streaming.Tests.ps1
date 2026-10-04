Describe 'evx query streaming contracts' {
    BeforeAll {
        $script:StreamingCliPath = $null
        $CliCandidates = @(
            $env:EVX_CLI_PATH
            Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Release\net10.0\evx.exe'
            Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Debug\net10.0\evx.exe'
        )
        foreach ($Candidate in $CliCandidates) {
            if (-not [string]::IsNullOrWhiteSpace($Candidate) -and (Test-Path -LiteralPath $Candidate)) {
                $script:StreamingCliPath = $Candidate
                break
            }
        }
        $script:StreamingFixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        if (-not $script:StreamingCliPath) {
            throw 'Build EventViewerX.Cli for net10.0 or set EVX_CLI_PATH before running CLI tests.'
        }
    }

    It 'preserves snapshot rows and completion evidence in native and forward portable streams' -TestCases @(
        @{ ReaderOptions = @() }
        @{ ReaderOptions = @('--portable-evtx') }
    ) {
        param($ReaderOptions)
        $SnapshotSummary = Join-Path $TestDrive 'snapshot.json'
        $StreamSummary = Join-Path $TestDrive 'stream.json'
        $QueryOptions = @('query', '--path', $script:StreamingFixturePath, '--oldest', '--max', '2',
            '--read-mode', 'StructuredData', '--require-complete') + $ReaderOptions
        $Snapshot = @(& $script:StreamingCliPath @QueryOptions --summary-file $SnapshotSummary)
        $LASTEXITCODE | Should -Be 2
        $Stream = @(& $script:StreamingCliPath @QueryOptions --stream --summary-file $StreamSummary)
        $LASTEXITCODE | Should -Be 2

        $Stream | Should -Be $Snapshot
        $Stream.Count | Should -Be 2
        (Get-Content -LiteralPath $StreamSummary -Raw) | Should -Be (Get-Content -LiteralPath $SnapshotSummary -Raw)
        (Get-Content -LiteralPath $StreamSummary -Raw | ConvertFrom-Json).IsComplete | Should -BeFalse
    }

    It 'rejects portable reverse streaming before opening input or an executable' -TestCases @(
        @{ ReaderOptions = @('--portable-evtx') }
        @{ ReaderOptions = @('--portable-evtx-executable', 'missing-reader.exe') }
    ) {
        param($ReaderOptions)
        $SummaryPath = Join-Path $TestDrive 'rejected-summary.json'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:StreamingCliPath query --path missing.evtx --stream @ReaderOptions --summary-file $SummaryPath 2>&1
            $ExitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }
        $ExitCode | Should -Be 1
        [string] $Output | Should -Match 'requires --oldest'
        Test-Path -LiteralPath $SummaryPath | Should -BeFalse
    }
}
