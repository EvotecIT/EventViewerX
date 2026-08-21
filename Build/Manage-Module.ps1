param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $false,

    [string] $PowerShellGalleryApiKeyPath = 'C:\Support\Important\PowerShellGalleryAPI.txt',

    [string] $GitHubApiKeyPath = 'C:\Support\Important\GitHubAPI.txt'
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force

Build-Module -ModuleName 'PSWinReportingV2' {
    $Manifest = [ordered] @{
        ModuleVersion        = '2.0.24'
        CompatiblePSEditions = @('Desktop', 'Core')
        GUID                 = 'ea2bd8d2-cca1-4dc3-9e1c-ff80b06e8fbe'
        Author               = 'Przemyslaw Klys'
        CompanyName          = 'Evotec'
        Copyright            = "(c) 2011 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        Description          = "PSWinReportingV2 is a fast and efficient event viewing, reporting, and collection tool. It is version 2 of PSWinReporting and can be installed alongside it."
        Tags                 = @('PSWinReporting', 'ActiveDirectory', 'Events', 'Reporting', 'Windows', 'EventLog')
        IconUri              = 'https://evotec.xyz/wp-content/uploads/2018/10/PSWinReporting.png'
        ProjectUri           = 'https://github.com/EvotecIT/EventViewerX'
        PowerShellVersion    = '5.1'
    }
    New-ConfigurationManifest @Manifest

    # The final frozen release carries the compatible v1 event-query engine
    # privately because later PSEventViewer versions changed its contract.
    New-ConfigurationModule -Type RequiredModule -Name 'PSWriteExcel' -Guid '82232c6a-27f1-435d-a496-929f7221334b' -RequiredVersion '0.1.15'
    New-ConfigurationModule -Type RequiredModule -Name 'PSWriteHTML' -Guid 'a7bdf640-f5cb-4acf-9de0-365b322d245c' -RequiredVersion '1.41.0'

    New-ConfigurationModule -Type ApprovedModule -Name 'PSSharedGoods' -RequiredVersion '0.0.312'
    New-ConfigurationModule -Type ApprovedModule -Name 'PSWriteColor' -RequiredVersion '1.0.3'

    New-ConfigurationModuleSkip -IgnoreModuleName @(
        'Microsoft.PowerShell.Management'
        'Microsoft.PowerShell.Security'
        'Microsoft.PowerShell.Utility'
        'Microsoft.WSMan.Management'
        'ActiveDirectory'
        'NetTCPIP'
        'PSWriteExcel'
        'ScheduledTasks'
        'TeamsX'
        'PSTeams'
        'PSSlack'
        'PSDiscord'
        'dbatools'
    ) -IgnoreFunctionName @(
        'ConvertTo-Excel'
        'eventChannel'
        'eventID'
        'eventRecordID'
        'eventSeverity'
        # Nested inside the frozen legacy event-query scriptblock.
        'Get-EventsInternal'
        'Initialize-XPathFilter'
        'Join-XPathFilter'
        'New-SlackMessage'
        'New-SlackMessageAttachment'
        'Send-SlackMessage'
        'New-TeamsFact'
        'New-TeamsSection'
        'Send-TeamsMessage'
        'New-DiscordFact'
        'New-DiscordImage'
        'New-DiscordSection'
        'Send-DiscordMessage'
    )

    $ConfigurationFormat = [ordered] @{
        RemoveComments                              = $true
        RemoveEmptyLines                            = $true
        PlaceOpenBraceEnable                        = $true
        PlaceOpenBraceOnSameLine                    = $true
        PlaceOpenBraceNewLineAfter                  = $true
        PlaceOpenBraceIgnoreOneLineBlock            = $true
        PlaceCloseBraceEnable                       = $true
        PlaceCloseBraceNewLineAfter                 = $false
        PlaceCloseBraceIgnoreOneLineBlock           = $true
        PlaceCloseBraceNoEmptyLineBefore            = $false
        UseConsistentIndentationEnable              = $true
        UseConsistentIndentationKind                = 'space'
        UseConsistentIndentationPipelineIndentation = 'IncreaseIndentationAfterEveryPipeline'
        UseConsistentIndentationIndentationSize     = 4
        UseConsistentWhitespaceEnable               = $true
        UseConsistentWhitespaceCheckInnerBrace      = $true
        UseConsistentWhitespaceCheckOpenBrace       = $true
        UseConsistentWhitespaceCheckOpenParen       = $true
        UseConsistentWhitespaceCheckOperator        = $true
        UseConsistentWhitespaceCheckPipe            = $true
        UseConsistentWhitespaceCheckSeparator       = $true
        AlignAssignmentStatementEnable              = $true
        AlignAssignmentStatementCheckHashtable      = $true
        UseCorrectCasingEnable                      = $true
    }
    New-ConfigurationFormat -ApplyTo 'OnMergePSM1', 'OnMergePSD1' -Sort None @ConfigurationFormat
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'DefaultPSM1' -EnableFormatting -Sort None
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'OnMergePSD1' -PSD1Style 'Minimal'

    New-ConfigurationBuild -Enable -SignModule:$SignModule -MergeModuleOnBuild -MergeFunctionsFromApprovedModules -ResolveMissingModulesOnline -DeleteTargetModuleBeforeBuild -CertificateThumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'

    New-ConfigurationArtefact -Type Unpacked -Enable -Path 'Artefacts\Unpacked' -ModulesPath 'Modules' -AddRequiredModules -RequiredModulesSource Download -RequiredModulesRepository 'PSGallery'
    New-ConfigurationArtefact -Type Packed -Enable -Path 'Artefacts\Packed' -IncludeTagName

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubApiKeyPath -UserName 'EvotecIT' -RepositoryName 'EventViewerX' -Enabled:$false -GenerateReleaseNotes -OverwriteTagName '{ModuleName}-v{ModuleVersionWithPreRelease}'

    New-ConfigurationGate -Mode $RunMode
}
