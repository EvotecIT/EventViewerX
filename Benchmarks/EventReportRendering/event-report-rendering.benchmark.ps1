$fixturePath = Get-BenchmarkInput -Name FixturePath
$rowCounts = (Get-BenchmarkInput -Name RowCounts -Default '1000,10000').Split(',')
New-BenchmarkSuite 'event-report-rendering' {
    Add-BenchmarkCaseSource @(foreach ($text in $rowCounts) {
        [int] $count = 0
        if (-not [int]::TryParse($text, [ref] $count) -or $count -lt 100) { throw 'Row counts must be integers of at least 100.' }
        foreach ($format in @('Html', 'Email')) { [pscustomobject] @{ Name = "$format-$count"; Rows = $count; Format = $format } }
    })
    Set-BenchmarkPolicy -Warmup 1 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata Contract 'Deterministic generic rows; fixture generation excluded; HTML retains all rows; email displays the first 25.'
    Add-BenchmarkEngine EventViewerXReporting {
        Add-BenchmarkOperation Render {
            param($case, $run)
            $output = & dotnet $fixturePath $case.Rows $case.Format
            if ($LASTEXITCODE -ne 0) { throw 'The reporting fixture failed.' }
            $run.Result = $output | ConvertFrom-Json
        }
    }
    Add-BenchmarkValidation {
        param($case, $run)
        Assert-BenchmarkValue -Actual ([int] $run.Result.Rows) -Expected ([int] $case.Rows) -Message 'The snapshot must retain its full count.'
        Assert-BenchmarkValue -Actual $run.Result.HasFirst -Expected $true -Message 'The first record must be rendered.'
        Assert-BenchmarkValue -Actual $run.Result.HasLast -Expected ($case.Format -eq 'Html') -Message 'Email must respect the 25-row digest bound.'
        Assert-BenchmarkValue -Actual $run.Result.HasLimitRow -Expected ($case.Format -eq 'Html') -Message 'The first row beyond the digest bound must be absent from email.'
    }
    Add-BenchmarkMetric RenderMs { param($case, $run) $run.Result.RenderMs }
    Add-BenchmarkMetric RendererAllocatedBytes { param($case, $run) $run.Result.AllocatedBytes }
    Add-BenchmarkMetric OutputBytes { param($case, $run) $run.Result.OutputBytes }
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
