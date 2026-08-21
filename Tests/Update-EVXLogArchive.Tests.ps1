Describe 'Update-EVXLogArchive' {
    BeforeAll {
        $Command = Get-Command Update-EVXLogArchive
    }

    It 'exports the compiled cmdlet' {
        $Command.CommandType | Should -Be 'Cmdlet'
        $Command.Parameters.Keys | Should -Contain 'Path'
        $Command.Parameters.Keys | Should -Contain 'Culture'
        $Command.Parameters.Keys | Should -Contain 'WhatIf'
    }

    It 'honors WhatIf without requiring the file to exist' {
        {
            Update-EVXLogArchive -Path (
                Join-Path $TestDrive 'not-created.evtx'
            ) -WhatIf
        } | Should -Not -Throw
    }
}
