Import-Module PSEventViewer -Force

$LogName = 'Contoso-App'
$SourceName = 'Contoso-App-Source'

New-EVXLog `
    -LogName $LogName `
    -ProviderName $SourceName `
    -MaximumKilobytes 20480 `
    -OverflowAction OverwriteAsNeeded

Set-EVXLog `
    -LogName $LogName `
    -MaximumSizeMB 32 `
    -Mode Circular

Get-EVXLog -LogName $LogName
Test-EVXLog -LogName $LogName -MaxEventsToScan 10

Clear-EVXLog `
    -LogName $LogName `
    -BackupPath 'C:\EventBackups\Contoso-App.evtx' `
    -Confirm:$false

Remove-EVXLog -LogName $LogName -Confirm:$false
