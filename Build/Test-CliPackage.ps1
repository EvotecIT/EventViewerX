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
            @{ Name = 'Event type contract'; FileName = '{ToolPath}'; Arguments = @('types', '--type', 'ADUserLogonFailed'); OutputJsonKind = 'Object'; OutputContains = @('"Name":"ADUserLogonFailed"', '"EventIds":[4625]') }
            @{
                Name = 'Portable EVTX query and ingestion'; FileName = '{ToolPath}'
                Arguments = @('query', '--path', '{ProjectRoot}/Tests/Logs/NamedFilterExamples.evtx', '--portable-evtx', '--oldest', '--max', '1', '--write-store', '{WorkRoot}/events.db')
                OutputJsonKind = 'Object'; OutputContains = @('"EventId":7040', '"RecordId":37', '"param1":"SmsRouter"')
                NonEmptyFiles = @('{WorkRoot}/events.db')
            }
            @{
                Name = 'Stored evidence reload'; FileName = '{ToolPath}'
                Arguments = @('query', '--store', '{WorkRoot}/events.db', '--max', '1')
                OutputJsonKind = 'Object'; OutputContains = @('"EventId":7040', '"RecordId":37', '"param1":"SmsRouter"')
            }
            @{
                Name = 'Stored evidence integrity'; FileName = '{ToolPath}'
                Arguments = @('store', 'integrity', '--path', '{WorkRoot}/events.db')
                OutputJsonKind = 'Object'; OutputContains = @('"IsHealthy":true', '"EventCount":1')
            }
        )
    }
}
