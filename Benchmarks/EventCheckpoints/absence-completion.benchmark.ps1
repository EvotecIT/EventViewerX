$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
New-BenchmarkSuite 'absence-completion' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\AbsenceCompletion') {
    Add-BenchmarkCaseSource @([pscustomobject]@{ Name = 'Expired-10000'; Count = 10000 })
    Set-BenchmarkPolicy -Warmup 1 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata Contract '10000 late completions after 10000 expired starts in one group; setup and final evidence validation excluded'
    Set-BenchmarkSetup { param($case, $run) $run.Fixture = [EventViewerX.Benchmarks.EventAbsenceBenchmarkFixture]::new($case.Count) }
    Add-BenchmarkEngine Current {
        Add-BenchmarkOperation Match { param($case, $run) $run.Fixture.MatchCompletions() }
    }
    Add-BenchmarkValidation {
        param($case, $run)
        Assert-BenchmarkValue -Actual $run.Fixture.Validate() -Expected $case.Count -Message 'Every expired trigger must remain available as absence evidence.'
        $run.Fixture = $null
    }
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
