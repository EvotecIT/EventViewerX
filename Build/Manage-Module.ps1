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

Build-Module -ModuleName 'PSWinReporting' {
    # Usual defaults as per standard module
    $Manifest = [ordered] @{
        # Version number of this module.
        ModuleVersion = '1.8.1.7'
        # ID used to uniquely identify this module
        GUID          = '4b446d15-93e7-4eec-a6ee-d741f2ae2f3b'
        # Author of this module
        Author        = 'Przemyslaw Klys'
        # Company or vendor of this module
        CompanyName   = 'Evotec'
        # Copyright statement for this module
        Copyright     = "(c) 2011 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        # Description of the functionality provided by this module
        Description   = 'This PowerShell Module, which started as an event library (Get-EventsLibrary.ps1), has now grown up and became full fledged PowerShell Module. This module has multiple functionalities but one of the signature features of this module is ability to parse Security (mostly) logs on Domain Controllers.'
        # Tags applied to this module. These help with module discovery in online galleries.
        Tags          = @('Windows', 'PSWinReporting', 'ActiveDirectory', 'Events', 'Reporting')
        IconUri       = 'https://evotec.xyz/wp-content/uploads/2018/10/PSWinReporting.png'
        ProjectUri    = 'https://github.com/EvotecIT/EventViewerX'
    }
    New-ConfigurationManifest @Manifest #-Prerelease "Alpha02"

    # The final frozen release carries the compatible v1 event-query engine
    # privately because later PSEventViewer versions changed its contract.
    New-ConfigurationModule -Type RequiredModule -Name 'PSWriteExcel' -Guid '82232c6a-27f1-435d-a496-929f7221334b' -RequiredVersion '0.1.15'
    New-ConfigurationModule -Type RequiredModule -Name 'PSSharedGoods' -Guid 'ee272aa8-baaa-4edf-9f45-b6d6f7d844fe' -RequiredVersion '0.0.312'
    New-ConfigurationModule -Type RequiredModule -Name 'PSWriteColor' -Guid '0b0ba5c5-ec85-4c2b-a718-874e55a8bc3f' -RequiredVersion '1.0.3'
    New-ConfigurationModule -Type ExternalModule -Name 'ActiveDirectory'

    New-ConfigurationModuleSkip -IgnoreModuleName @(
        # this are builtin into PowerShell, so not critical
        'Microsoft.PowerShell.Management'
        'Microsoft.PowerShell.Security'
        'Microsoft.PowerShell.Utility'
        'ScheduledTasks'
        'PSWriteExcel'
        'TeamsX'
        # this is optional, and checked for existance in the source codes directly
        'PSTeams'
        'PSSlack'
        'dbatools'
    ) -IgnoreFunctionName @(
        'ConvertTo-Excel'
        # those functions are internal within private function
        'Select-Unique', 'Compare-TwoArrays', 'IsNumeric', 'IsOfType', 'Format-HTML', 'Optimize-HTML'
        # Special nofunctions
        'eventChannel'
        'eventID'
        'eventRecordID'
        'eventSeverity'
        # Nested inside the frozen legacy event-query scriptblock.
        'Get-EventsFilter'
        'Get-EventsInternal'
        'ConvertTo-XPathXmlLiteral'
        'Initialize-XPathFilter'
        'Join-XPathFilter'
        # slack
        'New-SlackMessage'
        'New-SlackMessageAttachment'
        'New-TeamsFact'
        'New-TeamsSection'
        'Send-SlackMessage'
        # dbatools
        'Invoke-DbaQuery'
    )

    $ConfigurationFormat = [ordered] @{
        RemoveComments                              = $true
        RemoveEmptyLines                            = $true

        PlaceOpenBraceEnable                        = $true
        PlaceOpenBraceOnSameLine                    = $true
        PlaceOpenBraceNewLineAfter                  = $true
        PlaceOpenBraceIgnoreOneLineBlock            = $false

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
    # format PSD1 and PSM1 files when merging into a single file
    # enable formatting is not required as Configuration is provided
    New-ConfigurationFormat -ApplyTo 'OnMergePSM1', 'OnMergePSD1' -Sort None @ConfigurationFormat
    # format PSD1 and PSM1 files within the module
    # enable formatting is required to make sure that formatting is applied (with default settings)
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'DefaultPSM1' -EnableFormatting -Sort None
    # when creating PSD1 use special style without comments and with only required parameters
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'OnMergePSD1' -PSD1Style 'Minimal'
    # configuration for documentation, at the same time it enables documentation processing
    New-ConfigurationBuild -Enable -SignModule:$SignModule -MergeModuleOnBuild -ResolveMissingModulesOnline -DeleteTargetModuleBeforeBuild -CertificateThumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'

    #New-ConfigurationTest -TestsPath "$PSScriptRoot\..\Tests" -Enable

    New-ConfigurationArtefact -Type Unpacked -Enable -Path "$PSScriptRoot\..\Artefacts\Unpacked" -AddRequiredModules -RequiredModulesSource Download -RequiredModulesRepository 'PSGallery'
    New-ConfigurationArtefact -Type Packed -Enable -Path "$PSScriptRoot\..\Artefacts\Packed" -ArtefactName '<ModuleName>.v<ModuleVersion>.zip'

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubApiKeyPath -UserName 'EvotecIT' -RepositoryName 'EventViewerX' -Enabled:$false -GenerateReleaseNotes -OverwriteTagName '{ModuleName}-v{ModuleVersionWithPreRelease}'

    New-ConfigurationGate -Mode $RunMode
}
