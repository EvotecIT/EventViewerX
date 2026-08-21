Import-Module PSEventViewer -Force

# Stream recent local events with deterministic English messages.
Get-EVXEvent `
    -LogName System `
    -EventId 12, 13 `
    -TimePeriod Last24Hours `
    -ReadMode Message `
    -MessageCulture en-US `
    -MaxEvents 100 |
    Select-Object TimeCreated, Id, ProviderName, Message

# Query every channel linked to a provider without supplying LogName.
Get-EVXEvent `
    -ProviderName Microsoft-Windows-Kernel-General `
    -EventId 12 `
    -ReadMode Metadata `
    -MaxEvents 10

# Fan out across remote computers with bounded source concurrency.
Get-EVXEvent `
    -LogName Security `
    -MachineName DC01, DC02 `
    -EventId 4740 `
    -MaxConcurrency 4 `
    -ContinueOnError `
    -MaxEvents 100

# Get-WinEvent-compatible hashtables support named EventData keys.
Get-EVXEvent -FilterHashtable @{
    LogName = 'Security'
    Id = 4625
    StartTime = (Get-Date).AddHours(-1)
    TargetUserName = 'alice'
} -MaxEvents 50

# Prefer a typed filter when several EVX workflows share the same selection.
$FailedLogons = New-EVXFilter `
    -EventId 4625 `
    -TimePeriod Last24Hours `
    -NamedDataExcludeFilter @{ TargetUserName = 'svc_legacy' }
Get-EVXEvent `
    -LogName Security `
    -Filter $FailedLogons `
    -ReadMode StructuredData `
    -MaxEvents 100
