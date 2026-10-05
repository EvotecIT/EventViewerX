Describe 'Investigation and incremental detection workflows' {
    BeforeAll {
        $Raw = @(Get-EVXEvent -Path (Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx') -Oldest -MaxEvents 3)
        $Definition = [EventViewerX.EventDetectionRuleDefinition]::new()
        $Definition.RuleId = 'test:restart'
        $Definition.Title = 'Two events'
        $Definition.Kind = [EventViewerX.EventDetectionRuleKind]::Threshold
        $Definition.Threshold = 2
        $Definition.Window = [TimeSpan]::FromDays(1)
        $Rule = [EventViewerX.EventDetectionRule]::new($Definition)
        $Plan = [EventViewerX.EventDetectionPlan]::Compile([EventViewerX.IEventDetectionRule[]] @($Rule), $null)
    }

    It 'resumes pending correlation and refuses to overwrite or misidentify a checkpoint' {
        $Checkpoint = Join-Path $TestDrive 'first.json'
        @($Raw[0] | Invoke-EVXDetection -Rule $Rule -Stream -CheckpointOut $Checkpoint -SourceIdentity 'fixture/all' -RetentionIdentity '1').Count | Should -Be 0
        $Result = @($Raw[1] | Invoke-EVXDetection -Rule $Rule -Stream -CheckpointIn $Checkpoint -SourceIdentity 'fixture/all' -RetentionIdentity '1')
        $Result.Count | Should -Be 1
        $Result[0].Evidence.Count | Should -Be 2
        { $Raw[0] | Invoke-EVXDetection -Rule $Rule -Stream -CheckpointOut $Checkpoint -SourceIdentity 'fixture/all' -RetentionIdentity '1' -ErrorAction Stop } | Should -Throw
        { $Raw[1] | Invoke-EVXDetection -Rule $Rule -Stream -CheckpointIn $Checkpoint -SourceIdentity 'fixture/all' -RetentionIdentity '2' -ErrorAction Stop } | Should -Throw
    }

    It 'captures and replays a session without changing original input evidence' {
        $Manifest = [EventViewerX.EventInvestigationManifest]::new()
        $Manifest.StartUtc = $Raw[0].TimeCreated.ToUniversalTime().AddMinutes(-1)
        $Manifest.EndUtc = $Raw[-1].TimeCreated.ToUniversalTime().AddMinutes(1)
        $Manifest.QueryIdentity = 'fixture/oldest-three'
        $Manifest.ParserVersions.Add('EventViewerX', [EventViewerX.EventObject].Assembly.GetName().Version.ToString())
        $InputPath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $Before = (Get-FileHash -LiteralPath $InputPath).Hash
        $SessionPath = Join-Path $TestDrive 'investigation'
        $Session = $Raw | Export-EVXInvestigation -Path $SessionPath -Manifest $Manifest -Plan $Plan -InputPath $InputPath
        $Replay = Open-EVXInvestigation -Path $SessionPath | Invoke-EVXInvestigation
        $Replay.Observations.Count | Should -Be $Raw.Count
        $Replay.Findings.Count | Should -Be 1
        $Session.Manifest.Artifacts.Count | Should -Be 4
        (Get-FileHash -LiteralPath $InputPath).Hash | Should -Be $Before
        Add-Content -LiteralPath (Join-Path $SessionPath 'observations.jsonl') -Value 'changed'
        { Open-EVXInvestigation -Path $SessionPath -ErrorAction Stop } | Should -Throw
    }

    It 'compares the same pack against one shared historical sample without claiming undeclared coverage' {
        $Pack = Get-EVXDetectionPack | Select-Object -First 1
        $Observations = foreach ($Event in $Raw) { [EventViewerX.EventObservation]::Create($Event, $null, $null, $null) }
        $Preview = $Observations | Compare-EVXDetectionPack -Previous $Pack -Current $Pack -Historical -MaximumObservations 2
        $Preview.ObservationCount | Should -Be 2
        $Preview.FindingCountChange | Should -Be 0
        $Preview.Appearing.Count | Should -Be 0
        $Preview.Disappearing.Count | Should -Be 0
        $Preview.IsComplete | Should -BeFalse
    }

    It 'projects raw investigation events using the typed plan before durable capture' {
        $Saved = [EventViewerX.SavedEventRecord]::new()
        $Saved.EventId = 12
        $Saved.ProviderName = 'Microsoft-Windows-Kernel-General'
        $Saved.Channel = 'System'
        $Saved.Computer = 'server01'
        $Saved.RecordId = 1
        $Saved.TimeCreatedUtc = [DateTime]::UtcNow.AddMinutes(-1)
        $RestoreFixture = [EventViewerX.SavedEventRecord].GetMethod('ToEventObject', [Reflection.BindingFlags]'Instance,NonPublic')
        $Source = $RestoreFixture.Invoke($Saved, @('System', [EventViewerX.EventReadMode]::Full))
        $TypedDefinition = [EventViewerX.EventDetectionRuleDefinition]::new()
        $TypedDefinition.RuleId = 'test:typed-startup'
        $TypedDefinition.Title = 'Startup'
        $TypedDefinition.EventTypes = [EventViewerX.EventType[]] @([EventViewerX.EventType]::OSStartup)
        $TypedRule = [EventViewerX.EventDetectionRule]::new($TypedDefinition)
        $TypedPlan = [EventViewerX.EventDetectionPlan]::Compile([EventViewerX.IEventDetectionRule[]] @($TypedRule), $null)
        $Expected = @($Source | Invoke-EVXDetection -Rule $TypedRule)
        $Expected.Count | Should -Be 1
        $Manifest = [EventViewerX.EventInvestigationManifest]::new()
        $Manifest.StartUtc = $Saved.TimeCreatedUtc
        $Manifest.EndUtc = $Saved.TimeCreatedUtc.AddMinutes(1)
        $Manifest.QueryIdentity = 'startup-fixture'
        $Session = $Source | Export-EVXInvestigation -Path (Join-Path $TestDrive 'typed-session') -Manifest $Manifest -Plan $TypedPlan
        $Replay = $Session | Invoke-EVXInvestigation
        $Replay.Findings.Count | Should -Be $Expected.Count
        $Replay.Observations[0].TypeName | Should -Be 'OSStartup'
    }
}

Describe 'Watcher accepted-action draining' {
    It 'stops on action overload while allowing its accepted action to drain' {
        $Watcher = Start-EVXWatcher -LogName System -FilterXPath '*' -ReadMode Metadata -Start Oldest -ActionCapacity 1 -Action { Start-Sleep -Milliseconds 200 }
        try {
            $Deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not $Watcher.DrainCompletion.IsCompleted -and [DateTime]::UtcNow -lt $Deadline) { Start-Sleep -Milliseconds 25 }
            $Watcher.Health.IsOverloaded | Should -BeTrue
            $Watcher.Health.Rejected | Should -Be 1
            $Watcher.Health.Acknowledged | Should -Be 1
            $Watcher.Health.Completed | Should -Be 1
            $Watcher.DrainCompletion.IsCompleted | Should -BeTrue
        } finally { Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue }
    }

    It 'caps projected callbacks before dispatch and drains accepted work' {
        $DefinitionPath = Join-Path $TestDrive 'system-definition.json'
        @{
            Name = 'SystemProbe'
            Sources = @(@{ LogName = 'System'; EventIds = @(7040, 7036, 6005, 6006) })
            Fields = @(@{ Name = 'Payload'; Source = 'Data'; SourceName = 'param1' })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        $Watcher = Start-EVXWatcher -Definition $DefinitionPath -Start Oldest -StopAfter 1 -ActionCapacity 2 -Action { Start-Sleep -Milliseconds 100 }
        try {
            $Deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not $Watcher.DrainCompletion.IsCompleted -and [DateTime]::UtcNow -lt $Deadline) { Start-Sleep -Milliseconds 25 }
            $Watcher.DrainCompletion.IsCompleted | Should -BeTrue
            $Watcher.Health.Acknowledged | Should -Be 1
            $Watcher.Health.Completed | Should -Be 1
            $Watcher.Health.Queued | Should -Be 0
            $Watcher.Health.SourceLag | Should -Not -BeNullOrEmpty
        } finally { Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue }
    }

    It 'counts nonterminating action errors as failures and still acknowledges completion' {
        $Watcher = Start-EVXWatcher -LogName System -FilterXPath '*' -Start Oldest -StopAfter 1 -Action { Write-Error 'Expected action test failure'; 'after error' }
        try {
            $Deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not $Watcher.DrainCompletion.IsCompleted -and [DateTime]::UtcNow -lt $Deadline) { Start-Sleep -Milliseconds 25 }
            $Watcher.DrainCompletion.IsCompleted | Should -BeTrue
            $Watcher.Health.Failed | Should -Be 1
            $Watcher.Health.Acknowledged | Should -Be 1
        } finally { Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue }
    }

    It 'rejects projected reuse when the group delivery policy changes' {
        $Name = 'EVX.Policy.' + [Guid]::NewGuid().ToString('N')
        $Watcher = Start-EVXWatcher -Name $Name -Type OSStartup -Action {} -ActionIdentity 'same-action' -StopAfter 2
        try {
            { Start-EVXWatcher -Name $Name -Type OSStartup -Action {} -ActionIdentity 'same-action' -StopAfter 3 -ErrorAction Stop } | Should -Throw
            { Start-EVXWatcher -Name $Name -Type OSStartup -Action {} -ActionIdentity 'same-action' -StopAfter 2 -ActionCapacity 4 -ErrorAction Stop } | Should -Throw
        } finally { Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue }
    }
}
