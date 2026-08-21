describe 'Remove-EVXSource cmdlet' {
    BeforeAll {
        $script:source = 'TestEVXSource' + [Guid]::NewGuid().ToString('N')
        $script:isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
        $script:skip = -not $script:isAdmin
    }

    BeforeEach {
        if (-not $script:skip) {
            New-EVXSource `
                -LogName 'Application' `
                -SourceName $script:source |
                Out-Null
            Write-EVXEvent -LogName 'Application' -ProviderName $script:source -Id 1 -Message 'test'
        }
    }

    AfterAll {
        try { Remove-EVXSource -SourceName $script:source -LogName 'Application' -ErrorAction SilentlyContinue | Out-Null } catch { }
    }

    It 'removes existing source' -Skip:$script:skip {
        $result = Remove-EVXSource -SourceName $script:source -LogName 'Application'
        if ($script:isAdmin) {
            $result | Should -Be $true
            Remove-EVXSource -SourceName $script:source -LogName 'Application' | Should -Be $false
        }
        else {
            $result | Should -Be $false
        }
    }

    It 'emits no false success value when using -WhatIf' -Skip:$script:skip {
        Remove-EVXSource -SourceName $script:source -LogName 'Application' -WhatIf |
            Should -BeNullOrEmpty
        [EventViewerX.ClassicEventLogManager]::SourceExists(
            $script:source,
            'Application') | Should -BeTrue
    }
}
