Describe 'Find-WinEvent integration (local)' {
    BeforeAll {
        $localMachine = $env:COMPUTERNAME
    }

    It 'lists Application/System logs without error' {
        $logs = Get-EVXLog -LogName 'Application','System' -MachineName $localMachine
        $logs | Should -Not -BeNullOrEmpty
        $logs.LogName | Should -Contain 'Application'
        $logs.LogName | Should -Contain 'System'
    }

    It 'queries System log events without throwing' {
        { Find-WinEvent -Verbose -LogName 'System' -MachineName $localMachine -MaxEvents 1 | Select-Object -First 1 } | Should -Not -Throw
    }

    It 'queries Application log events without throwing' {
        { Find-WinEvent -Verbose -LogName 'Application' -MachineName $localMachine -MaxEvents 1 | Select-Object -First 1 } | Should -Not -Throw
    }

    It 'returns events from Application locally when MachineName is omitted' {
        $events = Find-WinEvent -Verbose -LogName 'Application' -MaxEvents 2
        $events | Should -Not -BeNullOrEmpty
        $events[0].LogName | Should -Be 'Application'
        $events[1].LogName | Should -Be 'Application'
    }

    It 'runs on local machine without MachineName specified' {
        $events = Find-WinEvent -Verbose -LogName 'System' -MaxEvents 2
        $events | Should -Not -BeNullOrEmpty
        $events[0].LogName | Should -Be 'System'
        $events[1].LogName | Should -Be 'System'
    }

    It 'queries an event type without throwing (OS startup)' {
        { Find-WinEvent -Verbose -Type OSStartup -MachineName $localMachine -MaxEvents 1 | Select-Object -First 1 } | Should -Not -Throw
    }
}
