Describe 'Write-EVXEvent classic writer' {
    It 'keeps the Write-EVXEntry Source migration syntax' {
        $Source = "EVXMigration$([guid]::NewGuid().ToString('N'))"

        {
            Write-EVXEntry `
                -LogName Application `
                -Source $Source `
                -EventId 1000 `
                -Message 'Migration preview' `
                -WhatIf
        } | Should -Not -Throw
        [EventViewerX.ClassicEventLogManager]::SourceExists(
            $Source,
            'Application') | Should -BeFalse
    }

    It 'throws terminating error with -ErrorAction Stop when write fails' {
        {
            Write-EVXEvent -LogName Application -ProviderName TestProvider `
                -Message 'Test message' -Id 1000 -Category 40000 -ErrorAction Stop
        } | Should -Throw
    }

    It 'writes a nonterminating ErrorRecord that honors ErrorVariable' {
        $CapturedErrors = @()
        Write-EVXEvent -LogName Application -ProviderName TestProvider `
            -Message 'Test message' -Id 1000 -Category 40000 `
            -ErrorAction SilentlyContinue -ErrorVariable +CapturedErrors

        $CapturedErrors.Count | Should -Be 1
        $CapturedErrors[0].FullyQualifiedErrorId |
            Should -Match 'EVXClassicEventWriteFailed'
    }

    It 'does not register a missing source during an ordinary write' {
        $Source = "EVXMissing$([guid]::NewGuid().ToString('N'))"

        {
            Write-EVXEvent `
                -LogName Application `
                -ProviderName $Source `
                -Message 'No implicit registration' `
                -Id 1000 `
                -ErrorAction Stop
        } | Should -Throw '*not registered*'
        [EventViewerX.ClassicEventLogManager]::SourceExists(
            $Source,
            'Application') | Should -BeFalse
    }

    It 'does not write or register under WhatIf' {
        $Source = "EVXWhatIf$([guid]::NewGuid().ToString('N'))"

        Write-EVXEvent `
            -LogName Application `
            -ProviderName $Source `
            -Message 'Preview only' `
            -Id 1000 `
            -CreateSource `
            -WhatIf

        [EventViewerX.ClassicEventLogManager]::SourceExists(
            $Source,
            'Application') | Should -BeFalse
    }
}

Describe 'Write-EVXEvent manifest provider writer' {
    It 'exports a thin schema-aware manifest writer' {
        $Command = Get-Command Write-EVXEvent

        $Command.CommandType | Should -Be 'Cmdlet'
        $Command.Parameters.Keys | Should -Contain 'ProviderName'
        $Command.Parameters.Keys | Should -Contain 'Id'
        $Command.Parameters.Keys | Should -Contain 'Version'
        $Command.Parameters.Keys | Should -Contain 'Payload'
        $Command.Parameters.Keys | Should -Contain 'WhatIf'
    }

    It 'validates payload count before writing' {
        {
            Write-EVXEvent `
                -ProviderName Microsoft-Windows-PowerShell `
                -Id 4100 `
                -Payload @('only-one') `
                -Confirm:$false `
                -ErrorAction Stop
        } | Should -Throw '*expects 3 payload value*'
    }

    It 'writes a registered event that both engines read identically' {
        $Marker = 'EVX-' + [guid]::NewGuid().ToString('N')
        $StartTime = (Get-Date).AddSeconds(-1)

        $Result = Write-EVXEvent `
            -ProviderName Microsoft-Windows-PowerShell `
            -Id 4100 `
            -Payload @('Context', $Marker, 'Payload') `
            -Confirm:$false `
            -ErrorAction Stop

        $EVXEvent = $null
        $WinEvent = $null
        $EVXFilter = New-EVXFilter `
            -ProviderName Microsoft-Windows-PowerShell `
            -EventId 4100 `
            -StartTime $StartTime
        foreach ($Attempt in 1..10) {
            $EVXEvent = Get-EVXEvent `
                -LogName Microsoft-Windows-PowerShell/Operational `
                -Filter $EVXFilter `
                -ReadMode Full `
                -MaxEvents 20 |
                Where-Object { $_.Data.Values -contains $Marker } |
                Select-Object -First 1
            $WinEvent = Get-WinEvent `
                -FilterHashtable @{
                    LogName = 'Microsoft-Windows-PowerShell/Operational'
                    ProviderName = 'Microsoft-Windows-PowerShell'
                    Id = 4100
                    StartTime = $StartTime
                } `
                -MaxEvents 20 `
                -ErrorAction SilentlyContinue |
                Where-Object { $_.Properties.Value -contains $Marker } |
                Select-Object -First 1
            if ($EVXEvent -and $WinEvent) {
                break
            }
            Start-Sleep -Milliseconds 100
        }

        $Result.Success | Should -BeTrue
        $Result.NativeStatus | Should -Be 0
        $Result.Definition.LogName |
            Should -Be 'Microsoft-Windows-PowerShell/Operational'
        $Result.Definition.PayloadFields.Name |
            Should -Be @('ContextInfo', 'UserData', 'Payload')
        $EVXEvent | Should -Not -BeNullOrEmpty
        $WinEvent | Should -Not -BeNullOrEmpty
        $EVXEvent.Data.Values |
            Should -Be $WinEvent.Properties.Value
    }
}
