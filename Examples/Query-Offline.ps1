Import-Module PSEventViewer -Force

$EvtxPath = 'C:\Logs\Security.evtx'

# Metadata is the lowest-allocation object projection for large scans.
Get-EVXEvent `
    -Path $EvtxPath `
    -Oldest `
    -ReadMode Metadata |
    Select-Object TimeCreated, RecordId, Id, ProviderName, MachineName

# StructuredData provides typed properties, XML, and named EventData without
# paying for provider message formatting.
Get-EVXEvent `
    -Path $EvtxPath `
    -Oldest `
    -ReadMode StructuredData `
    -NamedDataFilter @{ TargetUserName = 'alice' } `
    -MaxEvents 100 |
    Select-Object TimeCreated, Id, Data
