Describe 'New-EVXLog cmdlet' {
    BeforeAll {
        $suffix = [Guid]::NewGuid().ToString('N')
        $script:log = 'EVX' + $suffix + 'TestLog'
        $script:provider = 'EVXTestSource' + $suffix
        $script:isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
        $script:skip = -not $script:isAdmin
    }
    It 'creates new log with provider' -Skip:$script:skip {
        try {
            if ([EventViewerX.ClassicEventLogManager]::LogExists($script:log)) {
                [EventViewerX.ClassicEventLogManager]::RemoveLog($script:log) | Out-Null
            }

            $result = New-EVXLog -LogName $script:log -ProviderName $script:provider -MaximumKilobytes 1024 -OverflowAction OverwriteAsNeeded
            $result.CreatedLog | Should -BeTrue
            $result.CreatedSource | Should -BeTrue
            $result.After.LogExists | Should -BeTrue
            $result.After.SourceExists | Should -BeTrue
            [System.Diagnostics.EventLog]::Exists($script:log) | Should -Be $true
            $info = Get-EVXLog -LogName $script:log
            $info.LogName | Should -Be $script:log
            Remove-EVXLog -LogName $script:log | Should -Be $true
            [System.Diagnostics.EventLog]::Exists($script:log) | Should -Be $false
        } finally {
            if ([EventViewerX.ClassicEventLogManager]::LogExists($script:log)) {
                [EventViewerX.ClassicEventLogManager]::RemoveLog($script:log) | Out-Null
            }
        }
    }
}
