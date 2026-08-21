Describe 'PSEventViewer v4 command surface' {
    BeforeAll {
        $ExpectedCommands = @(
            'Clear-EVXLog'
            'Export-EVXEvent'
            'Get-EVXCollectorSubscription'
            'Get-EVXEvent'
            'Get-EVXLog'
            'Get-EVXPowerShellScript'
            'Get-EVXProvider'
            'Get-EVXWatcher'
            'Install-EVXProviderPackage'
            'New-EVXCollectorSubscription'
            'New-EVXFilter'
            'New-EVXLog'
            'New-EVXProviderPackage'
            'New-EVXSource'
            'Remove-EVXLog'
            'Remove-EVXSource'
            'Reset-EVXEventCheckpoint'
            'Set-EVXCollectorSubscription'
            'Set-EVXLog'
            'Show-EVXEvent'
            'Start-EVXWatcher'
            'Stop-EVXWatcher'
            'Test-EVXLog'
            'Test-EVXProviderDefinition'
            'Uninstall-EVXProviderPackage'
            'Update-EVXLogArchive'
            'Write-EVXEvent'
        )
    }

    It 'exports only the canonical cmdlets' {
        $Actual = Get-Command -Module PSEventViewer -CommandType Cmdlet |
            Select-Object -ExpandProperty Name |
            Sort-Object

        $Actual | Should -Be ($ExpectedCommands | Sort-Object)
    }

    It 'keeps only deliberate migration aliases' {
        $Aliases = Get-Command -Module PSEventViewer -CommandType Alias |
            Select-Object -ExpandProperty Name |
            Sort-Object

        $Aliases | Should -Be @(
            'Find-WinEvent'
            'Get-EVXFilter'
            'Write-EVXEntry'
        )
        (Get-Alias Find-WinEvent).ResolvedCommandName | Should -Be 'Get-EVXEvent'
        (Get-Alias Get-EVXFilter).ResolvedCommandName | Should -Be 'New-EVXFilter'
        (Get-Alias Write-EVXEntry).ResolvedCommandName | Should -Be 'Write-EVXEvent'
    }

    It 'does not re-export superseded duplicate workflows' {
        foreach ($Name in @(
                'ConvertTo-EVXProviderDefinition'
                'Get-EVXEventStatistics'
                'Get-EVXPowerShellScriptExecution'
                'Get-EVXProviderPackage'
            )) {
            Get-Command -Name $Name -Module PSEventViewer -ErrorAction SilentlyContinue |
                Should -BeNullOrEmpty
        }
    }

    It 'declares the packaged module as AMD64' {
        $Module = Get-Module PSEventViewer
        $Manifest = Import-PowerShellDataFile -Path (Join-Path $Module.ModuleBase 'PSEventViewer.psd1')

        $Manifest.ProcessorArchitecture | Should -Be 'Amd64'
    }

    It 'uses the expected managed cmdlet architecture for the selected payload' {
        $Module = Get-Module PSEventViewer
        $AssemblyPath = $Module.ExportedCommands['Get-EVXEvent'].ImplementingType.Assembly.Location
        $Assembly = [System.Reflection.Assembly]::LoadFile($AssemblyPath)
        $PEKind = [System.Reflection.PortableExecutableKinds]::NotAPortableExecutableImage
        $Machine = [System.Reflection.ImageFileMachine]::I386
        $Assembly.ManifestModule.GetPEKind([ref] $PEKind, [ref] $Machine)

        ($PEKind -band [System.Reflection.PortableExecutableKinds]::ILOnly) | Should -Not -Be 0
        ($PEKind -band [System.Reflection.PortableExecutableKinds]::Required32Bit) | Should -Be 0
        $IsCoreDevelopmentPayload = $PSVersionTable.PSEdition -eq 'Core' -and
            $AssemblyPath -like '*\Sources\PSEventViewer\bin\*'
        if ($IsCoreDevelopmentPayload) {
            ($PEKind -band [System.Reflection.PortableExecutableKinds]::PE32Plus) | Should -Be 0
            $Machine | Should -Be ([System.Reflection.ImageFileMachine]::I386)
        } else {
            ($PEKind -band [System.Reflection.PortableExecutableKinds]::PE32Plus) | Should -Not -Be 0
            $Machine | Should -Be ([System.Reflection.ImageFileMachine]::AMD64)
        }
    }

    It 'declares both collector subscription result shapes' {
        $OutputTypes = (Get-Command Set-EVXCollectorSubscription).OutputType.Name

        $OutputTypes | Should -Contain 'EventViewerX.CollectorSubscriptionUpdateResult'
        $OutputTypes | Should -Contain 'EventViewerX.CollectorSubscriptionRemovalResult'
        $OutputTypes | Should -Contain 'EventViewerX.CollectorSubscriptionSnapshot'
    }

    It 'has valid and intentional parameter sets on every canonical cmdlet' {
        foreach ($Command in Get-Command -Module PSEventViewer -CommandType Cmdlet) {
            { $Command.ParameterSets.Count } | Should -Not -Throw
            $Command.ParameterSets.Count | Should -BeGreaterThan 0
        }

        (Get-Command Get-EVXEvent).ParameterSets.Name | Sort-Object |
            Should -Be (@('Channel', 'Path', 'Definition', 'Provider', 'Type', 'TypedFilter', 'Hashtable', 'Xml') | Sort-Object)
        (Get-Command Show-EVXEvent).ParameterSets.Name | Sort-Object |
            Should -Be (@('Type', 'Path', 'Log', 'Definition', 'Input', 'Store') | Sort-Object)
        (Get-Command New-EVXFilter).ParameterSets.Name | Sort-Object |
            Should -Be (@('Object', 'XPath', 'ChannelXml', 'FileXml', 'Type', 'Definition') | Sort-Object)
        (Get-Command Get-EVXPowerShellScript).ParameterSets.Name | Sort-Object |
            Should -Be (@('Script', 'Execution') | Sort-Object)
        (Get-Command Write-EVXEvent).ParameterSets.Name |
            Should -Contain 'Classic'
        $WriteCommand = Get-Command Write-EVXEvent
        ($WriteCommand.ParameterSets |
                Where-Object Name -EQ 'Classic').Parameters.Name |
            Should -Not -Contain 'Version'
        foreach ($Name in 'ByIdPayload', 'ByIdData', 'ByNameData') {
            ($WriteCommand.ParameterSets |
                    Where-Object Name -EQ $Name).Parameters.Name |
                Should -Contain 'Version'
        }
        $WriteCommand.Parameters.ProviderName.Aliases |
            Should -Contain 'Source'
        $WriteCommand.Parameters.ProviderName.Aliases |
            Should -Contain 'Provider'
    }
}
