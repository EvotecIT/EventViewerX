[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ModuleArchive,
    [string] $Version,
    [ValidateSet('powershell', 'pwsh')]
    [string[]] $Hosts = @('powershell', 'pwsh')
)

Import-Module PSPublishModule -ErrorAction Stop

Invoke-ReleaseValidation -Version $Version -ProjectRoot (Split-Path -Parent $PSScriptRoot) -Settings {
    New-ConfigurationReleaseValidation -Modules @{
        Path = $ModuleArchive
        ArchiveRoot = 'PSEventViewer'
        Manifest = 'PSEventViewer.psd1'
        ProcessorArchitecture = 'None'
        RequiredFiles = @(
            'PSEventViewer.psm1'
            'Lib/Default/PSEventViewer.dll'
            'Lib/Standard/PSEventViewer.dll'
            'Lib/Standard/runtimes/win-x64/native/e_sqlite3.dll'
            'Lib/Standard/runtimes/win-x86/native/e_sqlite3.dll'
            'Lib/Standard/runtimes/win-arm64/native/e_sqlite3.dll'
        )
        VersionedAssemblies = @('Lib/Default/EventViewerX*.dll', 'Lib/Standard/EventViewerX*.dll')
        ProbeScript = 'Build/Test-ModuleRuntime.ps1'
        Hosts = $Hosts
    }
}
