@{
    AliasesToExport      = @()
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @()
    CompanyName          = 'Evotec'
    Copyright            = '(c) 2011 - 2026 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'PSWinReportingV2 is a fast and efficient event viewing, reporting, and collection tool. It is version 2 of PSWinReporting and can be installed alongside it.'
    FunctionsToExport    = @('Add-EventsDefinitions', 'Add-WinTaskScheduledForwarder', 'Find-Events', 'New-WinSubscriptionTemplates', 'Remove-WinTaskScheduledForwarder', 'Start-WinNotifications', 'Start-WinReporting', 'Start-WinSubscriptionService')
    GUID                 = 'ea2bd8d2-cca1-4dc3-9e1c-ff80b06e8fbe'
    ModuleVersion        = '2.0.24'
    PrivateData          = @{
        PSData = @{
            Tags                       = @('PSWinReporting', 'ActiveDirectory', 'Events', 'Reporting', 'Windows', 'EventLog')
            ProjectUri                 = 'https://github.com/EvotecIT/EventViewerX'
            IconUri                    = 'https://evotec.xyz/wp-content/uploads/2018/10/PSWinReporting.png'
            RequireLicenseAcceptance   = $false
            ExternalModuleDependencies = @()
        }
    }
    RequiredModules      = @(@{
            Guid            = '82232c6a-27f1-435d-a496-929f7221334b'
            ModuleName      = 'PSWriteExcel'
            RequiredVersion = '0.1.15'
        }, @{
            Guid            = 'a7bdf640-f5cb-4acf-9de0-365b322d245c'
            ModuleName      = 'PSWriteHTML'
            RequiredVersion = '1.41.0'
        }, @{
            Guid            = 'ee272aa8-baaa-4edf-9f45-b6d6f7d844fe'
            ModuleName      = 'PSSharedGoods'
            RequiredVersion = '0.0.312'
        }, @{
            Guid            = '0b0ba5c5-ec85-4c2b-a718-874e55a8bc3f'
            ModuleName      = 'PSWriteColor'
            RequiredVersion = '1.0.3'
        })
    RootModule           = 'PSWinReportingV2.psm1'
    PowerShellVersion    = '5.1'
    CompatiblePSEditions = @('Desktop', 'Core')
    ScriptsToProcess     = @()
}
