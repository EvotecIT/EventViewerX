Describe 'Set-EVXLog channel policy' {
    BeforeAll {
        $suffix = [Guid]::NewGuid().ToString('N')
        $script:log = 'EVX' + $suffix + 'Limit'
        $script:provider = 'EVXLimitSource' + $suffix
        $script:isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
        $script:skip = -not $script:isAdmin
    }
    It 'applies size and retention mode through one typed result' -Skip:$script:skip {
        try {
            $configuration = [EventViewerX.ClassicEventLogConfiguration]::new()
            $configuration.LogName = $script:log
            $configuration.SourceName = $script:provider
            [EventViewerX.ClassicEventLogManager]::EnsureLog($configuration) | Out-Null

            $result = Set-EVXLog -LogName $script:log -MaximumSizeMB 2 -Mode Retain
            $result.Success | Should -BeTrue

            $details = Get-EVXLog -LogName $script:log
            $details.MaximumSizeInBytes | Should -Be (2MB)
            $details.LogMode | Should -Be 'Retain'
        } finally {
            if ([EventViewerX.ClassicEventLogManager]::LogExists($script:log)) {
                [EventViewerX.ClassicEventLogManager]::RemoveLog($script:log) | Out-Null
            }
        }
    }
}
