Describe 'Set-EVXCollectorSubscription' {
    It 'exposes a confirmed local service-API write surface' {
        $Command = Get-Command Set-EVXCollectorSubscription
        $Command.Parameters.Keys | Should -Contain 'Name'
        $Command.Parameters.Keys | Should -Contain 'Enabled'
        $Command.Parameters.Keys | Should -Contain 'Remove'
        $Command.Parameters.Keys | Should -Contain 'InitializeCollector'
        $Command.Parameters.Keys | Should -Contain 'SkipWinRmQuickConfig'
        $Command.Parameters.Keys | Should -Contain 'WhatIf'
        $Command.Parameters.Keys | Should -Contain 'Confirm'
    }

    It 'allows collector initialization as part of definition apply' {
        $DefinitionSet = (Get-Command Set-EVXCollectorSubscription).ParameterSets |
            Where-Object Name -EQ 'Definition'

        $DefinitionSet.Parameters.Name | Should -Contain 'InitializeCollector'
        $DefinitionSet.Parameters.Name | Should -Contain 'SkipWinRmQuickConfig'
    }

    It 'does not create a missing subscription' {
        $Name = 'PSEventViewer-Missing-' +
            [Guid]::NewGuid().ToString('N')
        {
            Set-EVXCollectorSubscription `
                -Name $Name `
                -Enabled $true `
                -Confirm:$false `
                -ErrorAction Stop
        } | Should -Throw
        Get-EVXCollectorSubscription -Name $Name |
            Should -BeNullOrEmpty
    }

    It 'exposes removal through the existing cmdlet parameter sets' {
        $Command = Get-Command Set-EVXCollectorSubscription
        $Command.ParameterSets.Name | Should -Contain 'Remove'
        ($Command.ParameterSets |
                Where-Object Name -EQ 'Remove').Parameters.Name |
            Should -Contain 'Remove'

        Set-EVXCollectorSubscription `
            -Name 'PSEventViewer-WhatIf-Removal' `
            -Remove `
            -WhatIf |
            Should -BeNullOrEmpty
    }
}
