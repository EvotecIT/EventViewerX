$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$cliPath = Get-BenchmarkInput -Name EventViewerXCliPath -Default (Join-Path $repositoryRoot 'Sources\EventViewerX.Cli\bin\Release\net10.0\evx.exe')
$eventViewerXPath = Get-BenchmarkInput -Name EventViewerXPath -Default (Join-Path $repositoryRoot 'Sources\EventViewerX\bin\Release\net10.0-windows\EventViewerX.dll')
$burstCountsText = Get-BenchmarkInput -Name BurstCounts -Default '100,1000,10000'
[int[]] $burstCounts = @($burstCountsText.Split(',') | ForEach-Object {
        [int] $value = 0
        if (-not [int]::TryParse($_.Trim(), [ref] $value) -or $value -le 0) {
            throw "BurstCounts must contain positive 32-bit values. Received '$($_)'."
        }
        $value
    } | Sort-Object -Unique)

Add-Type -Path $eventViewerXPath
$cliHash = (Get-FileHash -LiteralPath $cliPath -Algorithm SHA256).Hash
$coreHash = (Get-FileHash -LiteralPath $eventViewerXPath -Algorithm SHA256).Hash

New-BenchmarkSuite 'event-watcher-burst' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventWatcher') {
    Add-BenchmarkCaseSource @($burstCounts | ForEach-Object {
            [pscustomobject]@{
                Name = "Burst-$_"
                EventCount = $_
            }
        })
    Set-BenchmarkPolicy -Warmup 0 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup KeepOnFailure
    Add-BenchmarkMetadata EventViewerXCliSha256 $cliHash
    Add-BenchmarkMetadata EventViewerXSha256 $coreHash
    Add-BenchmarkMetadata Contract 'Ready-to-complete end-to-end delivery through a disposable native Windows Event Log'

    Set-BenchmarkSetup {
        param($case, $run)

        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 12)
        $run.LogName = "EVXBench$suffix"
        $run.SourceName = $run.LogName
        $run.WorkRoot = Join-Path ([IO.Path]::GetTempPath()) "evx-watch-$suffix"
        [IO.Directory]::CreateDirectory($run.WorkRoot) | Out-Null
        $run.DefinitionPath = Join-Path $run.WorkRoot 'definition.json'
        $run.ReadyPath = Join-Path $run.WorkRoot 'ready.json'
        $run.SummaryPath = Join-Path $run.WorkRoot 'summary.json'
        $run.JsonLinesPath = Join-Path $run.WorkRoot 'events.jsonl'

        $configuration = [EventViewerX.ClassicEventLogConfiguration]::new()
        $configuration.LogName = $run.LogName
        $configuration.SourceName = $run.SourceName
        $configuration.MaximumKilobytes = 65536
        $configuration.OverflowAction = [Diagnostics.OverflowAction]::OverwriteAsNeeded
        [EventViewerX.ClassicEventLogManager]::EnsureLog($configuration) | Out-Null

        $definition = @{
            Name = 'BurstEvent'
            DisplayName = 'Watcher burst event'
            Sources = @(@{
                    LogName = $run.LogName
                    EventIds = @(41000)
                    ProviderNames = @($run.SourceName)
                })
            Fields = @(@{
                    Name = 'Sequence'
                    Source = 'Message'
                    SourceName = ''
                })
        } | ConvertTo-Json -Depth 6
        [IO.File]::WriteAllText($run.DefinitionPath, $definition, [Text.UTF8Encoding]::new($false))

        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $cliPath
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        foreach ($argument in @(
                'watch', '--definition', $run.DefinitionPath,
                '--stop-after', [string] $case.EventCount,
                '--timeout', '00:02:00',
                '--ready-file', $run.ReadyPath,
                '--summary-file', $run.SummaryPath,
                '--jsonl', $run.JsonLinesPath)) {
            $startInfo.ArgumentList.Add($argument)
        }
        $run.Process = [Diagnostics.Process]::new()
        $run.Process.StartInfo = $startInfo
        if (-not $run.Process.Start()) {
            throw 'The EventViewerX watcher process did not start.'
        }
        $run.StandardOutputTask = $run.Process.StandardOutput.ReadToEndAsync()
        $run.StandardErrorTask = $run.Process.StandardError.ReadToEndAsync()

        $readyWait = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $run.ReadyPath -PathType Leaf)) {
            if ($run.Process.HasExited) {
                $stderr = $run.StandardErrorTask.GetAwaiter().GetResult()
                throw "The watcher exited before readiness with code $($run.Process.ExitCode): $stderr"
            }
            if ($readyWait.Elapsed -gt [TimeSpan]::FromSeconds(15)) {
                $run.Process.Kill($true)
                throw 'The watcher did not publish readiness within 15 seconds.'
            }
            Start-Sleep -Milliseconds 20
        }

        $run.Writer = [Diagnostics.EventLog]::new($run.LogName, '.', $run.SourceName)
    }

    Add-BenchmarkEngine EventViewerXCli {
        Add-BenchmarkOperation Deliver {
            param($case, $run)

            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            for ($index = 1; $index -le $case.EventCount; $index++) {
                $run.Writer.WriteEntry("Burst $index", [Diagnostics.EventLogEntryType]::Information, 41000)
            }
            if (-not $run.Process.WaitForExit(120000)) {
                $run.Process.Kill($true)
                throw "The watcher did not complete after receiving $($case.EventCount) events."
            }
            $run.StandardOutput = $run.StandardOutputTask.GetAwaiter().GetResult()
            $run.StandardError = $run.StandardErrorTask.GetAwaiter().GetResult()
            $stopwatch.Stop()
            $run.BurstElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            $run.ExitCode = $run.Process.ExitCode
        }
    }

    Add-BenchmarkValidation {
        param($case, $run)

        try {
            Assert-BenchmarkValue -Actual $run.ExitCode -Expected 0 -Message "Watcher stderr: $($run.StandardError)"
            Assert-BenchmarkPath $run.SummaryPath
            Assert-BenchmarkPath $run.JsonLinesPath
            $summary = Get-Content -LiteralPath $run.SummaryPath -Raw | ConvertFrom-Json
            Assert-BenchmarkValue -Actual ([int] $summary.Received) -Expected ([int] $case.EventCount) -Message 'Completion summary must account for every burst event.'
            $rows = @(Get-Content -LiteralPath $run.JsonLinesPath | ForEach-Object { $_ | ConvertFrom-Json })
            Assert-BenchmarkValue -Actual $rows.Count -Expected ([int] $case.EventCount) -Message 'JSONL output must contain every burst event exactly once.'
            Assert-BenchmarkValue -Actual @($rows.RecordId | Sort-Object -Unique).Count -Expected ([int] $case.EventCount) -Message 'Every delivered event record ID must be unique.'
        } finally {
            if ($run.Writer) {
                $run.Writer.Dispose()
            }
            if ($run.Process) {
                if (-not $run.Process.HasExited) {
                    $run.Process.Kill($true)
                    $run.Process.WaitForExit()
                }
                $run.Process.Dispose()
            }
            [EventViewerX.ClassicEventLogManager]::RemoveLog($run.LogName) | Out-Null
            if (Test-Path -LiteralPath $run.WorkRoot) {
                Remove-Item -LiteralPath $run.WorkRoot -Recurse -Force
            }
        }
    }

    Add-BenchmarkMetric EventsPerSecond {
        param($case, $run)
        [Math]::Round($case.EventCount / ($run.BurstElapsedMilliseconds / 1000), 2)
    }
    Add-BenchmarkMetric DeliveredEvents {
        param($case)
        $case.EventCount
    }
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
