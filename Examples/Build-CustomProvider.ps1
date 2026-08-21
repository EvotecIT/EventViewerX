param(
    [string] $DefinitionPath = "$PSScriptRoot\CustomProvider.definition.json",

    [string] $OutputPath = "$PSScriptRoot\Contoso.Scanner-1.0.0.evxprovider",

    [string] $CertificateThumbprint = '',

    [switch] $Install
)

$ErrorActionPreference = 'Stop'

Import-Module PSEventViewer -Force

$validation = Test-EVXProviderDefinition -Path $DefinitionPath
if (-not $validation.IsValid) {
    $validation.Errors | Format-List
    throw 'The custom provider definition is invalid.'
}

$buildParameters = @{
    DefinitionPath = $DefinitionPath
    OutputPath     = $OutputPath
    Force          = $true
    Confirm        = $false
}
if ($CertificateThumbprint) {
    $buildParameters.CertificateThumbprint = $CertificateThumbprint
}

$package = New-EVXProviderPackage @buildParameters
$package | Format-List

if ($Install) {
    $installParameters = @{
        Path    = $package.OutputPath
        Confirm = $false
    }
    if ($CertificateThumbprint) {
        $installParameters.TrustMode = 'RequireTrustedSignature'
        $installParameters.TrustedSignerThumbprint = $CertificateThumbprint
    }
    Install-EVXProviderPackage @installParameters | Format-List

    Write-EVXEvent `
        -ProviderName Contoso.Scanner `
        -EventName ScanCompleted `
        -Data @{
            ComputerName = $env:COMPUTERNAME
            FindingCount = 7
        } `
        -Confirm:$false
}
