Describe 'New-EVXSource explicit registration contract' {
    It 'is exported as the canonical source-registration command' {
        (Get-Command New-EVXSource).CommandType |
            Should -Be 'Cmdlet'
    }

    It 'does not register anything under WhatIf' {
        $Source = "EVXSourceWhatIf$([guid]::NewGuid().ToString('N'))"

        New-EVXSource `
            -SourceName $Source `
            -LogName Application `
            -WhatIf |
            Should -BeNullOrEmpty

        [EventViewerX.ClassicEventLogManager]::SourceExists(
            $Source,
            'Application') | Should -BeFalse
    }
}
