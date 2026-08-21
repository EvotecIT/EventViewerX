Import-Module PSEventViewer -Force

$Watcher = Start-EVXWatcher `
    -Name FailedLogons `
    -LogName Security `
    -EventId 4625 `
    -Start Future `
    -ReadMode Full `
    -StopAfter 10 `
    -TimeOut (New-TimeSpan -Minutes 30) `
    -Action {
        param($Event)
        $Event | Select-Object TimeCreated, Id, MachineName, Data
    }

Get-EVXWatcher -Id $Watcher.Id
Stop-EVXWatcher -Id $Watcher.Id -Confirm:$false
