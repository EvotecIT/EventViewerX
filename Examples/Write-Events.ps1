Import-Module PSEventViewer -Force

# Classic Event Log source. Registering a missing source is explicit.
Write-EVXEvent `
    -LogName Application `
    -ProviderName Contoso-App `
    -Id 1000 `
    -Message 'Service started' `
    -CreateSource `
    -Confirm:$false

# Registered manifest/ETW provider. The payload is validated and converted from
# the provider template before Windows receives it.
$Result = Write-EVXEvent `
    -ProviderName Microsoft-Windows-PowerShell `
    -Id 4100 `
    -Payload @('Context', 'User data', 'Payload') `
    -Confirm:$false

$Result | Select-Object Success, NativeStatus, PayloadCount,
    @{ Name = 'LogName'; Expression = { $_.Definition.LogName } }
