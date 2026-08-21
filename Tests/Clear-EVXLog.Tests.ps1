Describe 'Clear-EVXLog cmdlet' {
    BeforeAll {
        $suffix = [Guid]::NewGuid().ToString('N')
        $script:log = 'EVX' + $suffix + 'Clear'
        $script:provider = 'EVXClearSource' + $suffix
        $script:isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
        $script:skip = -not $script:isAdmin

    }
    It 'exposes bounded remote session controls' {
        $command = Get-Command Clear-EVXLog
        $command.Parameters.Keys | Should -Contain 'Credential'
        $command.Parameters.Keys | Should -Contain 'Authentication'
        $command.Parameters.Keys | Should -Contain 'TimeoutMs'
    }
    It 'atomically backs up and clears the log through wevtapi' -Skip:$script:skip {
        try {
            $configuration = [EventViewerX.ClassicEventLogConfiguration]::new()
            $configuration.LogName = $script:log
            $configuration.SourceName = $script:provider
            [EventViewerX.ClassicEventLogManager]::EnsureLog($configuration) | Out-Null

            $request = [EventViewerX.ClassicEventWriteRequest]::new()
            $request.LogName = $script:log
            $request.SourceName = $script:provider
            $request.Message = 'test'
            $request.EventId = 1000
            [EventViewerX.ClassicEventLogManager]::Write($request)

            $backup = Join-Path $TestDrive 'EVXClearTestLog.evtx'
            $result = Clear-EVXLog -LogName $script:log -BackupPath $backup -Confirm:$false

            $result.LogName | Should -Be $script:log
            $result.BackupPath | Should -Be ([IO.Path]::GetFullPath($backup))
            Test-Path -LiteralPath $backup | Should -BeTrue
            (Get-EVXLog -Path $backup).RecordCount | Should -BeGreaterThan 0
            (Get-EVXLog -LogName $script:log).RecordCount | Should -Be 0
        } finally {
            if ([EventViewerX.ClassicEventLogManager]::LogExists($script:log)) {
                [EventViewerX.ClassicEventLogManager]::RemoveLog($script:log) | Out-Null
            }
        }
    }
}
