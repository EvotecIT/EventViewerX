Describe 'Get-EVXLog canonical metadata surface' {
    It 'exposes Force for analytic and debug wildcard discovery' {
        (Get-Command Get-EVXLog).Parameters.Keys |
            Should -Contain 'Force'
    }

    It 'Should return some results' {
        $info = Get-EVXLog -MachineName $Env:COMPUTERNAME -LogName 'Application'
        $info | Should -Not -BeNullOrEmpty
        $info.MachineName | Should -Match ([regex]::Escape($Env:COMPUTERNAME))
        $info.LogName | Should -Be 'Application'
    }

    It 'reads native offline archive metadata without enumerating records' {
        $filePath = [io.path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $info1 = Get-EVXLog -Path $filePath
        Test-Path $filePath | Should -Be $true
        $info1 | Should -BeNullOrEmpty -Not
        $info1.Path | Should -Be ([IO.Path]::GetFullPath($filePath))
        $info1.RecordCount | Should -BeGreaterThan 0
        $info1.FileSize | Should -BeGreaterThan 0
    }
}
