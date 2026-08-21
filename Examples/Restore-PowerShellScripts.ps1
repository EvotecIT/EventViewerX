Import-Module PSEventViewer -Force

$Query = @{
    Edition = 'WindowsPowerShell'
    MachineName = 'DC01', 'DC02'
    OutputPath = 'C:\RecoveredScripts'
    MaxScripts = 100
    MaxEventsScanned = 50000
    MaxPendingScripts = 512
    MaxCachedEvents = 2048
    IncludeQueryInfo = $true
}
Get-EVXPowerShellScript @Query

Get-EVXPowerShellScript `
    -Execution `
    -Edition WindowsPowerShell `
    -MachineName DC01 `
    -MaxEvents 100 `
    -MaxEventsScanned 50000
