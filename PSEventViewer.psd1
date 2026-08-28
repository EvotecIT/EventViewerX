@{
    AliasesToExport       = @('Find-WinEvent', 'Get-EVXFilter', 'Write-EVXEntry')
    Author                = 'Przemyslaw Klys'
    CmdletsToExport       = @('Clear-EVXLog', 'Export-EVXEvent', 'Get-EVXCollectorSubscription', 'Get-EVXDetectionPack', 'Get-EVXEvent', 'Get-EVXLog', 'Get-EVXPowerShellScript', 'Get-EVXProvider', 'Get-EVXRequirement', 'Get-EVXTarget', 'Get-EVXWatcher', 'Import-EVXSigmaRule', 'Install-EVXProviderPackage', 'Invoke-EVXDetection', 'Measure-EVXEvent', 'New-EVXCollectorSubscription', 'New-EVXFilter', 'New-EVXLog', 'New-EVXProviderPackage', 'New-EVXSource', 'Remove-EVXLog', 'Remove-EVXSource', 'Reset-EVXEventCheckpoint', 'Set-EVXCollectorSubscription', 'Set-EVXLog', 'Show-EVXEvent', 'Start-EVXWatcher', 'Stop-EVXWatcher', 'Test-EVXLog', 'Test-EVXProviderDefinition', 'Test-EVXReadiness', 'Test-EVXSigmaRule', 'Uninstall-EVXProviderPackage', 'Update-EVXLogArchive', 'Write-EVXEvent')
    CompanyName           = 'Evotec'
    CompatiblePSEditions  = @('Desktop', 'Core')
    Copyright             = '(c) 2011 - 2026 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description           = 'High-performance typed Windows Event Log queries, reports, exports, watchers, WEC, custom providers, diagnostics, and administration for PowerShell.'
    FunctionsToExport     = @()
    GUID                  = '5df72a79-cdf6-4add-b38d-bcacf26fb7bc'
    ModuleVersion         = '4.0.0'
    PowerShellVersion     = '5.1'
    ProcessorArchitecture = 'Amd64'
    PrivateData           = @{
        PSData = @{
            ExternalModuleDependencies = @()
            IconUri                    = 'https://evotec.xyz/wp-content/uploads/2018/10/PSEventViewer.png'
            ProjectUri                 = 'https://github.com/EvotecIT/EventViewerX'
            RequireLicenseAcceptance   = $false
            Tags                       = @('Events', 'Viewer', 'Windows', 'XML', 'XPATH', 'EVTX', 'WEC', 'Reporting')
        }
    }
    RequiredModules       = @()
    RootModule            = 'PSEventViewer.psm1'
    ScriptsToProcess      = @()
}
