Import-Module PSEventViewer -Force

$Authentication = New-EVXFilter -Type ActiveDirectoryAuthentication
$Authentication.Fields | Get-Member -MemberType Property

Get-EVXEvent `
    -Type ADUserLogonFailed, ADUserLockouts `
    -MachineName DC01, DC02 `
    -TimePeriod Last24Hours `
    -ReadMode StructuredDataAndMessage `
    -Where { $_.Who -notlike 'NT AUTHORITY\*' } `
    -MaxEvents 500 |
    Select-Object TimeCreated, TypeName, MachineName, UserName, IpAddress

Get-EVXEvent `
    -Type ADSMBServerAuditV1 `
    -MachineName DC01, DC02 `
    -TimePeriod Last3Days `
    -ResolveDns `
    -DnsTimeoutMs 1000 `
    -DnsMaxConcurrency 8 |
    Select-Object When, Computer, ClientAddress, ClientDNSName,
        ClientDnsResolutionStatus
