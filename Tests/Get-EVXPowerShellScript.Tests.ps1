Describe 'Get-EVXPowerShellScript bounded query contract' {
    It 'Exposes output, scan, and bounded cache controls' {
        $parameters = (Get-Command -Name Get-EVXPowerShellScript -ErrorAction Stop).Parameters

        $parameters.ContainsKey('MaxScripts') | Should -BeTrue
        $parameters.ContainsKey('MaxEventsScanned') | Should -BeTrue
        $parameters.ContainsKey('MaxPendingScripts') | Should -BeTrue
        $parameters.ContainsKey('MaxCachedEvents') | Should -BeTrue
        $parameters.ContainsKey('IncludeQueryInfo') | Should -BeTrue
        $parameters['MaxScripts'].Aliases | Should -BeNullOrEmpty
    }

    It 'Rejects an unbounded incomplete-script cache configuration' {
        { Get-EVXPowerShellScript -Edition WindowsPowerShell -MaxPendingScripts 0 } | Should -Throw
    }

    It 'Rejects machine fan-out for a local offline event log' {
        $EventLogPath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        {
            Get-EVXPowerShellScript -Edition WindowsPowerShell -EventLogPath $EventLogPath -MachineName 'server-1', 'server-2'
        } | Should -Throw '*EventLogPath*cannot be combined with MachineName*'
        {
            Get-EVXPowerShellScript -Execution -Edition WindowsPowerShell -EventLogPath $EventLogPath -MachineName 'server-1'
        } | Should -Throw '*EventLogPath*cannot be combined with MachineName*'
    }

    It 'exposes execution records through one explicit parameter set' {
        $command = Get-Command -Name Get-EVXPowerShellScript -ErrorAction Stop

        $command.Parameters.ContainsKey('MaxEvents') | Should -BeTrue
        $command.Parameters.ContainsKey('MaxEventsScanned') | Should -BeTrue
        $command.Parameters.ContainsKey('IncludeQueryInfo') | Should -BeTrue
        $command.ParameterSets.Name | Should -Contain 'Execution'
    }
}
