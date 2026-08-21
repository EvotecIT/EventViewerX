Describe 'Get-EVXWatcher - missing watcher' {
    if (-not $IsWindows) { return }

    It 'returns nothing and does not throw when watcher is absent' {
        { Get-EVXWatcher -Name 'NonExistentWatcher' } | Should -Not -Throw
        Get-EVXWatcher -Name 'NonExistentWatcher' | Should -BeNullOrEmpty
    }
}
