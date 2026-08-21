Describe 'Test-EVXLog bounded probe' {
    It 'is exported with remote authentication and budget controls' {
        $Command = Get-Command Test-EVXLog

        $Command.CommandType | Should -Be 'Cmdlet'
        $Command.Parameters.Keys | Should -Contain 'Credential'
        $Command.Parameters.Keys | Should -Contain 'Authentication'
        $Command.Parameters.Keys | Should -Contain 'TimeoutMs'
        $Command.Parameters.Keys | Should -Contain 'MaxEventsToScan'
    }

    It 'returns a typed local System result without materializing full events' {
        $Result = Test-EVXLog -LogName System -TimeoutMs 5000 -MaxEventsToScan 10

        $Result | Should -Not -BeNullOrEmpty
        $Result.LogName | Should -Be 'System'
        $Result.Machine | Should -Not -BeNullOrEmpty
        $Result.Status.ToString() | Should -BeIn @('Ok', 'NoEvent', 'LimitReached')
        $Result.EventsScanned | Should -BeLessOrEqual 10
    }
}
