param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $true,

    [string] $ProjectBuildConfigPath = 'Sources\Build\project.build.json',

    [string] $PowerShellGalleryApiKeyPath = 'C:\Support\Important\PowerShellGalleryAPI.txt',

    [string] $GitHubApiKeyPath = 'C:\Support\Important\GitHubAPI.txt'
)

$ErrorActionPreference = 'Stop'

Import-Module PSPublishModule -Force

Build-Module -ModuleName 'PSEventViewer' {
    # Usual defaults as per standard module
    $Manifest = [ordered] @{
        # Version number of this module.
        ModuleVersion        = '4.0.X'
        # Supported PSEditions
        CompatiblePSEditions = @('Desktop', 'Core')
        # ID used to uniquely identify this module
        GUID                 = '5df72a79-cdf6-4add-b38d-bcacf26fb7bc'
        # Author of this module
        Author               = 'Przemyslaw Klys'
        # Company or vendor of this module
        CompanyName          = 'Evotec'
        # Copyright statement for this module
        Copyright            = "(c) 2011 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        # Description of the functionality provided by this module
        Description          = 'High-performance typed Windows Event Log queries, reports, exports, watchers, WEC, custom providers, diagnostics, and administration for PowerShell.'
        # Tags applied to this module. These help with module discovery in online galleries.
        Tags                 = @('Events', 'Viewer', 'Windows', 'XML', 'XPATH', 'EVTX', 'WEC', 'Reporting')

        # The Core payload is AnyCPU and selects RID-specific native assets at
        # runtime. The module entry script rejects non-x64 Desktop hosts.
        ProcessorArchitecture = 'None'

        IconUri              = 'https://evotec.xyz/wp-content/uploads/2018/10/PSEventViewer.png'

        ProjectUri           = 'https://github.com/EvotecIT/EventViewerX'

        PowerShellVersion    = '5.1'
    }
    New-ConfigurationManifest @Manifest

    $ConfigurationFormat = [ordered] @{
        RemoveComments                              = $false
        PlaceOpenBraceEnable                        = $true
        PlaceOpenBraceOnSameLine                    = $true
        PlaceOpenBraceNewLineAfter                  = $true
        PlaceOpenBraceIgnoreOneLineBlock            = $false
        PlaceCloseBraceEnable                       = $true
        PlaceCloseBraceNewLineAfter                 = $false
        PlaceCloseBraceIgnoreOneLineBlock           = $false
        PlaceCloseBraceNoEmptyLineBefore            = $true
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
    New-ConfigurationDocumentation -Enable -PathReadme 'Docs\Readme.md' -Path 'Docs' -SyncExternalHelpToProjectRoot
    $newConfigurationBuildSplat = @{
        Enable                            = $true
        SignModule                        = $SignModule
        MergeModuleOnBuild                = $true
        MergeFunctionsFromApprovedModules = $true
        CertificateThumbprint             = '92E95FB58EFFA6A4A75E77A33CDD6BFE6DD30F1A'
        ResolveBinaryConflicts            = $true
        ResolveBinaryConflictsName        = 'PSEventViewer'
        NETProjectName                    = 'PSEventViewer'
        NETProjectPath                    = 'Sources\PSEventViewer\PSEventViewer.csproj'
        NETConfiguration                  = 'Release'
        # The portable payload supports PowerShell 7.0+. Its project publish target
        # replaces the EventLog facade with the matching Windows runtime assembly.
        NETFramework                      = 'netstandard2.1', 'net472'
        NETSearchClass                    = 'PSEventViewer.CmdletGetEVXEvent'
        NETHandleAssemblyWithSameName     = $true
        NETAssemblyLoadContext            = $true
        NETHandleRuntimes                 = $true
        NETAssemblyTypeAcceleratorMode    = 'Assembly'
        NETAssemblyTypeAcceleratorAssemblies = @(
            'EventViewerX'
            'EventViewerX.Reporting'
            'EventViewerX.Storage'
            'EventViewerX.Detection'
            'EventViewerX.Evtx'
        )
        NETIgnoreLibraryOnLoad            = @(
            'HtmlForgeX.dll'
            'HtmlForgeX.Email.dll'
            'OfficeIMO.Core.dll'
            'OfficeIMO.CSV.dll'
            'OfficeIMO.Excel.dll'
            'DocumentFormat.OpenXml.dll'
            'DocumentFormat.OpenXml.Framework.dll'
            'System.IO.Packaging.dll'
        )
        NETBinaryModuleDocumentation      = $true
        #NETMergeLibraryDebugging          = $true
        DotSourceLibraries                = $true
        DotSourceClasses                  = $true
        DeleteTargetModuleBeforeBuild     = $true
    }

    New-ConfigurationBuild @newConfigurationBuildSplat

    New-ConfigurationProjectBuild -Name 'EventViewerX' -ConfigPath $ProjectBuildConfigPath -Enabled -BuildBeforeModule -UseAsReleaseVersionSource -ProvideLocalNuGetFeed -PublishNuget -PublishGitHub
    New-ConfigurationRelease -StageRoot 'Artefacts\UploadReady' -VersionSource ProjectBuild -PrimaryProject 'EventViewerX' -SynchronizeModuleVersion -BuildOrder 'Packages', 'Module' -PublishOrder 'NuGet', 'PowerShellGallery', 'GitHub'

    New-ConfigurationArtefact -Type Unpacked -Enable -Path 'Artefacts\Unpacked' -ModulesPath 'Modules'
    New-ConfigurationArtefact -Type Packed -Enable -Path 'Artefacts\Packed' -IncludeTagName

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false -UseAsDependencyVersionSource
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubApiKeyPath -UserName 'EvotecIT' -RepositoryName 'EventViewerX' -Enabled:$false -GenerateReleaseNotes -OverwriteTagName '{ModuleName}-v{ModuleVersionWithPreRelease}'

    New-ConfigurationGate -Mode $RunMode
}
