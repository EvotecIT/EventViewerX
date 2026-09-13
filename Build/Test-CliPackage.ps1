[CmdletBinding()]
param(
    [string] $PackageRoot = (Join-Path $PSScriptRoot '../Artefacts/ProjectBuild/packages'),
    [string] $Version
)

Import-Module PSPublishModule -ErrorAction Stop

Invoke-ReleaseValidation -Version $Version -ProjectRoot (Split-Path -Parent $PSScriptRoot) -Settings {
    New-ConfigurationReleaseValidation -Tools @{
        PackageId = 'EventViewerX.Cli'
        PackageRoot = $PackageRoot
        CommandName = 'evx'
        IncludeManifestInstall = $true
        Commands = @(
            @{ Name = 'Version'; FileName = '{ToolPath}'; Arguments = @('--version'); ExpectedOutput = '{Version}' }
            @{ Name = 'Help'; FileName = '{ToolPath}'; Arguments = @('--help'); OutputContains = @('EventViewerX {Version}', 'evx query') }
            @{ Name = 'Event types'; FileName = '{ToolPath}'; Arguments = @('types'); OutputJsonKind = 'Array'; MinimumJsonItems = 1 }
        )
    }
}
