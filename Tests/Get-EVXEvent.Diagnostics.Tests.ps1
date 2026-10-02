Describe 'Resolved event query plans and execution diagnostics' {
    It 'explains a raw channel query with consumer result and scan limits' {
        $plan = Get-EVXEvent -LogName System -MaxEvents 3 -MaxEventsScanned 10 -Explain
        $plan.Sources.Count | Should -BeGreaterThan 0
        $plan.ResultLimit | Should -Be 3
        $plan.CandidateLimit | Should -Be 10
    }
    It 'explains a typed query without requiring a predicate' {
        $plan = Get-EVXEvent -Type OSStartup -ReadMode Metadata -MaxEvents 3 -MaxEventsScanned 10 -Explain
        $plan | Should -BeOfType ([EventViewerX.EventQueryExplanation])
        $plan.Sources.Count | Should -BeGreaterThan 0
        $plan.ManagedStages[0] | Should -Match 'Typed projection'
        $plan.ResultLimit | Should -Be 3
        $plan.CandidateLimit | Should -Be 10
    }

    It 'explains native file queries without reading event contents or requiring Where' {
        $emptyFile = Join-Path $TestDrive 'empty.evtx'
        [IO.File]::WriteAllBytes($emptyFile, [byte[]] @())
        $plan = Get-EVXEvent -Path $emptyFile -EventId 1 -MaxEvents 10 -ReadMode Metadata -Oldest -Explain
        $plan | Should -BeOfType ([EventViewerX.EventQueryExplanation])
        $plan.Sources | Should -HaveCount 1
        $plan.Sources[0].Source | Should -Be $emptyFile
        $plan.Sources[0].NativeFilter | Should -Match 'EventID'
        $plan.Sources[0].ReadMode | Should -Be ([EventViewerX.EventReadMode]::Metadata)
        $plan.Oldest | Should -BeTrue
    }

    It 'reports native reads rejected by a managed message filter' {
        $info = $null
        $path = Join-Path $PSScriptRoot 'Logs/NamedFilterExamples.evtx'
        $events = @(Get-EVXEvent -Path $path -ReadMode Message -MaxEventsScanned 3 `
            -MessageRegex 'EVX-Test-No-Matching-Message-854291' -ExecutionInfo ([ref] $info))
        $events | Should -HaveCount 0
        $info.NativeEventsRead | Should -Be 3
        $info.ManagedRejections | Should -Be 3
        $info.EventsEmitted | Should -Be 0
        $info.SourceFailures | Should -Be 0
    }

    It 'reports the accepted event when a downstream command stops early' {
        $info = $null
        $path = Join-Path $PSScriptRoot 'Logs/NamedFilterExamples.evtx'
        $events = @(Get-EVXEvent -Path $path -ReadMode Metadata -ExecutionInfo ([ref] $info) | Select-Object -First 1)
        $events | Should -HaveCount 1
        $info.EventsEmitted | Should -Be 1
        $info.ManagedRejections | Should -Be 0
        $info.Completed | Should -BeFalse
    }
}
