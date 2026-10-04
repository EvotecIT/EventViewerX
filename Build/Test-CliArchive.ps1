<#
.SYNOPSIS
Checks a CLI archive on the current operating system and architecture.
.DESCRIPTION
Expands a locally built archive into a new caller-owned workspace and uses the
shared release validator to check queries, SQLite ingestion, reload, and integrity.
.PARAMETER ArchivePath
The FrameworkDependent or PortableCompat archive built for this host.
.PARAMETER WorkRoot
A new directory for the extracted payload and test database. Remove it after validation.
.PARAMETER Version
The expected three-part CLI version from the release manifest.
.EXAMPLE
./Build/Test-CliArchive.ps1 -ArchivePath ./Artefacts/UploadReady/Cli/cli/EventViewerX.Cli-net10.0-win-x64-PortableCompat.zip -WorkRoot ./Ignore/Validation/CliArchive -Version 4.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ArchivePath,
    [Parameter(Mandatory)] [string] $WorkRoot,
    [Parameter(Mandatory)] [string] $Version
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $WorkRoot) { throw 'Use a new workspace for each archive probe.' }
Import-Module PSPublishModule -ErrorAction Stop
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $WorkRoot
$windowsHost = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
$toolPath = Join-Path $WorkRoot $(if ($windowsHost) { 'evx.exe' } else { 'evx' })
if (-not $windowsHost) {
    & chmod u+x $toolPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not make the extracted CLI executable.' }
}
$storePath = Join-Path $WorkRoot 'events.db'
$fixturePath = Join-Path $PSScriptRoot '../Tests/Logs/NamedFilterExamples.evtx'
Invoke-ReleaseValidation -Version $Version -ProjectRoot (Split-Path -Parent $PSScriptRoot) -Settings {
    New-ConfigurationReleaseValidation -Commands @(
        @{ Name = 'Archive version'; FileName = $toolPath; Arguments = @('--version'); ExpectedOutput = $Version }
        @{ Name = 'Archive help'; FileName = $toolPath; Arguments = @('--help'); OutputContains = @("EventViewerX $Version", 'evx query') }
        @{ Name = 'Archive event type'; FileName = $toolPath; Arguments = @('types', '--type', 'ADUserLogonFailed'); OutputJsonKind = 'Object'; OutputContains = @('"Name":"ADUserLogonFailed"', '"EventIds":[4625]') }
        @{
            Name = 'Archive EVTX query and ingestion'; FileName = $toolPath
            Arguments = @('query', '--path', $fixturePath, '--portable-evtx', '--oldest', '--max', '1', '--write-store', $storePath)
            OutputJsonKind = 'Object'; OutputContains = @('"EventId":7040', '"RecordId":37', '"param1":"SmsRouter"'); NonEmptyFiles = @($storePath)
        }
        @{ Name = 'Archive stored evidence'; FileName = $toolPath; Arguments = @('query', '--store', $storePath, '--max', '1'); OutputJsonKind = 'Object'; OutputContains = @('"EventId":7040', '"RecordId":37', '"param1":"SmsRouter"') }
        @{ Name = 'Archive store integrity'; FileName = $toolPath; Arguments = @('store', 'integrity', '--path', $storePath); OutputJsonKind = 'Object'; OutputContains = @('"IsHealthy":true', '"EventCount":1') }
    )
}
