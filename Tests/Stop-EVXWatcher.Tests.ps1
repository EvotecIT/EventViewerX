Describe 'Start-EVXWatcher and Stop-EVXWatcher lifecycle' {
    if (-not $IsWindows) { return }

    BeforeAll {
        $script:watcher = Start-EVXWatcher -MachineName $env:COMPUTERNAME -LogName 'Application' -EventId 1 -Action {}
    }

    AfterAll {
        if ($script:watcher) {
            Stop-EVXWatcher -Id $script:watcher.Id -ErrorAction SilentlyContinue
        }
    }

    It 'registers the watcher' {
        $result = Get-EVXWatcher -Id $script:watcher.Id
        $result | Should -Not -BeNullOrEmpty
        $script:watcher.Watcher.StartTime | Should -Not -Be ([datetime]::MinValue)
        $script:watcher.EndTime | Should -Be $null
    }

    It 'stops the watcher and cleans up resources' {
        $StartTime = $script:watcher.Watcher.StartTime
        Stop-EVXWatcher -Id $script:watcher.Id
        $script:watcher.EndTime | Should -Not -Be $null
        $script:watcher.Watcher.StartTime | Should -Be $StartTime
        Get-EVXWatcher -Id $script:watcher.Id | Should -BeNullOrEmpty
    }
}
