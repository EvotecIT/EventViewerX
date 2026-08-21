Describe 'Start-EVXWatcher - Parameter validation' {
    It 'requires exactly one stop selector and supports ShouldProcess' {
        $command = Get-Command Stop-EVXWatcher
        $command.ParameterSets.Name | Should -Contain 'ById'
        $command.ParameterSets.Name | Should -Contain 'ByName'
        $command.ParameterSets.Name | Should -Contain 'All'
        $command.Parameters.Keys | Should -Contain 'WhatIf'
        $command.Parameters.Keys | Should -Contain 'PassThru'
        { Stop-EVXWatcher -ErrorAction Stop } | Should -Throw
        {
            Stop-EVXWatcher -Id ([Guid]::NewGuid()) -All -ErrorAction Stop
        } | Should -Throw
    }

    It 'Fails when NumberOfThreads is less than 1' {
        { Start-EVXWatcher -MachineName $env:COMPUTERNAME -LogName 'Application' -EventId 1 -Action {} -NumberOfThreads 0 } | Should -Throw
    }
    It 'Fails when NumberOfThreads is greater than 1024' {
        { Start-EVXWatcher -MachineName $env:COMPUTERNAME -LogName 'Application' -EventId 1 -Action {} -NumberOfThreads 2000 } | Should -Throw
    }

    It 'accepts event ID zero in an explicit watcher filter' {
        $Watcher = $null
        try {
            $Watcher = Start-EVXWatcher `
                -Name ('PSEventViewer.EventZero.' + [Guid]::NewGuid().ToString('N')) `
                -MachineName $env:COMPUTERNAME `
                -LogName Application `
                -EventId 0 `
                -Action {}

            $Watcher | Should -Not -BeNullOrEmpty
        } finally {
            if ($Watcher) {
                Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue
            }
        }
    }

    It 'exposes native XPath and hashtable subscription sets' {
        $sets = (Get-Command Start-EVXWatcher).ParameterSets.Name
        $sets | Should -Contain 'FilterXPath'
        $sets | Should -Contain 'FilterHashtable'
    }

    It 'lets a built-in Type own its source log and uses the optimized default read mode' {
        $Command = Get-Command Start-EVXWatcher
        $TypeSet = $Command.ParameterSets | Where-Object Name -EQ 'Type'
        $TypeSet.Parameters.Name | Should -Not -Contain 'LogName'
        $TypeSet.Parameters.Name | Should -Not -Contain 'Staging'

        $Watcher = Start-EVXWatcher -Type OSStartup -Action {}
        try {
            $Watcher.LogName | Should -Be 'System'
            $Watcher.SubscriptionQuery.ReadMode.ToString() |
                Should -Be 'StructuredDataAndMessage'
        } finally {
            Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue
        }
    }

    It 'lets a custom definition own its source log, provider, and event IDs' {
        $DefinitionPath = Join-Path $TestDrive 'service-change.json'
        @{
            Name = 'ServiceStartTypeChange'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param1'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $Watcher = Start-EVXWatcher -Definition $DefinitionPath -Action {}
        try {
            $Watcher.LogName | Should -Be 'System'
            $Watcher.Types.Count | Should -Be 0
            $Watcher.SubscriptionQuery.XPath | Should -Match 'EventID=7040'
            $Watcher.SubscriptionQuery.XPath | Should -Match 'Service Control Manager'
            $Watcher.SubscriptionQuery.ReadMode.ToString() |
                Should -Be 'StructuredDataAndMessage'
        } finally {
            Stop-EVXWatcher -Id $Watcher.Id -ErrorAction SilentlyContinue
        }
    }

    It 'requires bookmark start semantics' {
        {
            Start-EVXWatcher -LogName System -FilterXPath '*' -BookmarkXml '<BookmarkList />' -Action {}
        } | Should -Throw
    }

    It 'creates a bounded English-first native subscription from a hashtable' {
        $watcher = Start-EVXWatcher `
            -LogName System `
            -FilterHashtable @{ Id = 41; ProviderName = 'Microsoft-Windows-Kernel-Power' } `
            -Start Future `
            -ReadMode Metadata `
            -MessageCulture en-US `
            -BufferCapacity 8 `
            -Action {}
        try {
            $watcher.SubscriptionQuery.XPath | Should -Match 'EventID=41'
            $watcher.SubscriptionQuery.XPath | Should -Match 'Microsoft-Windows-Kernel-Power'
            $watcher.SubscriptionQuery.ReadMode.ToString() | Should -Be 'Metadata'
            $watcher.SubscriptionQuery.MessageCulture.Name | Should -Be 'en-US'
            $watcher.SubscriptionQuery.BufferCapacity | Should -Be 8
        } finally {
            Stop-EVXWatcher -Id $watcher.Id -ErrorAction SilentlyContinue
        }
    }

    It 'consolidates partitioned filters into one native union subscription' {
        $watcher = Start-EVXWatcher `
            -LogName System `
            -EventId (1..40) `
            -ReadMode Metadata `
            -Action {}
        try {
            $watcher.SubscriptionQueries.Count | Should -Be 1
            [xml] $QueryXml = $watcher.SubscriptionQuery.XPath
            @($QueryXml.QueryList.Query.Select).Count |
                Should -BeGreaterThan 1
        } finally {
            Stop-EVXWatcher -Id $watcher.Id -ErrorAction SilentlyContinue
        }
    }

    It 'expands provider wildcards before creating native subscriptions' {
        $watcher = Start-EVXWatcher `
            -LogName System `
            -FilterHashtable @{ ProviderName = 'Microsoft-Windows-Kernel-*' } `
            -ReadMode Metadata `
            -Action {}
        try {
            $watcher.SubscriptionQueries.Count | Should -BeGreaterThan 0
            ($watcher.SubscriptionQueries.XPath -join "`n") |
                Should -Match 'Microsoft-Windows-Kernel-'
            ($watcher.SubscriptionQueries.XPath -join "`n") |
                Should -Not -Match 'Microsoft-Windows-Kernel-\*'
        } finally {
            Stop-EVXWatcher -Id $watcher.Id -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Start-EVXWatcher - PowerShell event dispatch' {
    It 'queues detached events and preserves positional and pipeline action inputs' {
        $EventObject = Get-EVXEvent -LogName System -MaxEvents 1 -ReadMode Metadata | Select-Object -First 1
        if (-not $EventObject) {
            Set-ItResult -Skipped -Because 'The System event log contained no readable events.'
            return
        }

        $Assembly = [PSEventViewer.CmdletStartEVXWatcher].Assembly
        $BridgeType = $Assembly.GetType('PSEventViewer.PowerShellWatcherEventBridge', $true)
        $Bridge = [Activator]::CreateInstance($BridgeType, $true)
        $Flags = [Reflection.BindingFlags]'Static,NonPublic'
        $ActionBridge = $BridgeType.GetProperty('ActionScript', $Flags).GetValue($null)
        $Publish = $BridgeType.GetMethod('Publish', [Reflection.BindingFlags]'Instance,NonPublic')
        $Marker = Join-Path $TestDrive 'watcher-action.txt'
        $EscapedMarker = $Marker.Replace("'", "''")
        $UserAction = [ScriptBlock]::Create("param(`$EventObject) Set-Content -LiteralPath '$EscapedMarker' -Value (`$EventObject.Id.ToString() + '|' + `$_.Id.ToString())")
        $SourceIdentifier = 'PSEventViewer.Tests.' + [Guid]::NewGuid().ToString('N')
        $Subscriber = Register-ObjectEvent -InputObject $Bridge -EventName EventReceived -SourceIdentifier $SourceIdentifier -MessageData $UserAction -Action $ActionBridge

        try {
            $null = $Publish.Invoke($Bridge, @($EventObject.PSObject.BaseObject))
            $Deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not (Test-Path -LiteralPath $Marker) -and [DateTime]::UtcNow -lt $Deadline) {
                Start-Sleep -Milliseconds 50
            }

            Test-Path -LiteralPath $Marker | Should -BeTrue
            Get-Content -LiteralPath $Marker | Should -Be (([string] $EventObject.Id) + '|' + ([string] $EventObject.Id))
        } finally {
            Unregister-Event -SourceIdentifier $SourceIdentifier -ErrorAction SilentlyContinue
            if ($Subscriber.Action) {
                Remove-Job -Job $Subscriber.Action -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'reuses a named watcher only with an explicit stable action identity' {
        $Name = 'PSEventViewer.Tests.' + [Guid]::NewGuid().ToString('N')
        $BeforeCount = @(Get-EventSubscriber -Force | Where-Object SourceIdentifier -Like 'PSEventViewer.Watcher.*').Count
        $First = $null
        try {
            $First = Start-EVXWatcher -Name $Name -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -Action {} -ActionIdentity 'tests:stable-action'
            $Second = Start-EVXWatcher -Name $Name -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -Action {} -ActionIdentity 'tests:stable-action'

            $Second.Id | Should -Be $First.Id
            @(Get-EventSubscriber -Force | Where-Object SourceIdentifier -Like 'PSEventViewer.Watcher.*').Count |
                Should -Be ($BeforeCount + 1)
        } finally {
            if ($First) {
                Stop-EVXWatcher -Id $First.Id -ErrorAction SilentlyContinue
            }
        }
    }

    It 'rejects a recreated action delegate when no stable identity is supplied' {
        $Name = 'PSEventViewer.Tests.' + [Guid]::NewGuid().ToString('N')
        $First = $null
        try {
            $First = Start-EVXWatcher -Name $Name -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -Action {}
            { Start-EVXWatcher -Name $Name -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -Action {} } |
                Should -Throw
        } finally {
            if ($First) {
                Stop-EVXWatcher -Id $First.Id -ErrorAction SilentlyContinue
            }
        }
    }

    It 'removes the PowerShell bridge when an oldest-first watcher stops before registration settles' {
        $Name = 'PSEventViewer.AutoStop.' + [Guid]::NewGuid().ToString('N')
        $BeforeCount = @(Get-EventSubscriber -Force |
                Where-Object SourceIdentifier -Like 'PSEventViewer.Watcher.*').Count
        $Watcher = Start-EVXWatcher `
            -Name $Name `
            -MachineName $env:COMPUTERNAME `
            -LogName System `
            -FilterXPath '*' `
            -Start Oldest `
            -ReadMode Metadata `
            -StopAfter 1 `
            -Action {}
        try {
            $Deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not $Watcher.IsStopped -and
                   [DateTime]::UtcNow -lt $Deadline) {
                Start-Sleep -Milliseconds 25
            }

            $Watcher.IsStopped | Should -BeTrue
            $Watcher.EventsFound | Should -Be 1
            @(Get-EVXWatcher -Name $Name) | Should -BeNullOrEmpty
            while (@(Get-EventSubscriber -Force |
                        Where-Object SourceIdentifier -Like 'PSEventViewer.Watcher.*').Count -ne
                   $BeforeCount -and
                   [DateTime]::UtcNow -lt $Deadline) {
                Start-Sleep -Milliseconds 25
            }
            @(Get-EventSubscriber -Force |
                    Where-Object SourceIdentifier -Like 'PSEventViewer.Watcher.*').Count |
                Should -Be $BeforeCount
        } finally {
            [EventViewerX.WatcherManager]::StopWatcher($Watcher.Id) |
                Out-Null
        }
    }

    It 'stops watchers and removes bridge subscribers when the module is removed' {
        $ModulePath = (Get-Command -Name Start-EVXWatcher -CommandType Cmdlet).Module.Path
        $ExternalIds = [Collections.Generic.List[int]]::new()
        $ExternalIds.Add(1)
        $ExternalNamedEvents = [Collections.Generic.List[EventViewerX.EventType]]::new()
        $ExternalAction = [Action[EventViewerX.EventObject]] { param($EventObject) }
        $ExternalWatcher = [EventViewerX.WatcherManager]::StartWatcher(
            ('EventViewerX.External.' + [Guid]::NewGuid().ToString('N')),
            $env:COMPUTERNAME,
            'Application',
            $ExternalIds,
            $ExternalNamedEvents,
            $ExternalAction,
            $false,
            $false,
            0,
            $null)
        $Watcher = Start-EVXWatcher -Name ('PSEventViewer.Tests.' + [Guid]::NewGuid().ToString('N')) -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -Action {}

        try {
            Remove-Module PSEventViewer -Force

            $Watcher.EndTime | Should -Not -BeNullOrEmpty
            $ExternalWatcher.EndTime | Should -BeNullOrEmpty
            @(Get-EventSubscriber -Force | Where-Object SourceIdentifier -Like 'PSEventViewer.Watcher.*') | Should -BeNullOrEmpty
        } finally {
            [EventViewerX.WatcherManager]::StopWatcher($ExternalWatcher.Id) | Out-Null
            Import-Module -Name $ModulePath -Force
        }
    }

    It 'does not stop a watcher owned by another runspace module instance' {
        $ModulePath = (Get-Command -Name Start-EVXWatcher -CommandType Cmdlet).Module.Path
        $EscapedModulePath = $ModulePath.Replace("'", "''")
        $OtherRunspace = [RunspaceFactory]::CreateRunspace()
        $OtherPowerShell = [PowerShell]::Create()
        $MainWatcher = $null
        $OtherWatcher = $null
        try {
            $OtherRunspace.Open()
            $OtherPowerShell.Runspace = $OtherRunspace
            $SharedName = 'PSEventViewer.SharedRunspace.' + [Guid]::NewGuid().ToString('N')
            $ActionIdentity = 'PSEventViewer.Tests.SharedAction'
            $OtherScript = "Import-Module -Name '$EscapedModulePath' -Force; Start-EVXWatcher -Name '$SharedName' -MachineName '$env:COMPUTERNAME' -LogName Application -EventId 1 -ActionIdentity '$ActionIdentity' -Action {}"
            $OtherResult = $OtherPowerShell.AddScript($OtherScript).Invoke()
            if ($OtherPowerShell.HadErrors) {
                throw ($OtherPowerShell.Streams.Error | Select-Object -First 1)
            }
            $OtherWatcher = $OtherResult[0]
            $MainWatcher = Start-EVXWatcher -Name $SharedName -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -ActionIdentity $ActionIdentity -Action {}

            $MainWatcher.Id | Should -Not -Be $OtherWatcher.Id

            Remove-Module PSEventViewer -Force

            $MainWatcher.EndTime | Should -Not -BeNullOrEmpty
            $OtherWatcher.EndTime | Should -BeNullOrEmpty

            $OtherPowerShell.Commands.Clear()
            $null = $OtherPowerShell.AddScript('Remove-Module PSEventViewer -Force').Invoke()
            $OtherWatcher.EndTime | Should -Not -BeNullOrEmpty
        } finally {
            if ($MainWatcher -and -not $MainWatcher.EndTime) {
                [EventViewerX.WatcherManager]::StopWatcher($MainWatcher.Id) | Out-Null
            }
            if ($OtherWatcher -and -not $OtherWatcher.EndTime) {
                [EventViewerX.WatcherManager]::StopWatcher($OtherWatcher.Id) | Out-Null
            }
            $OtherPowerShell.Dispose()
            $OtherRunspace.Dispose()
            Import-Module -Name $ModulePath -Force
        }
    }

    It 'keeps module ownership independent from user-mutable global variables' {
        $ModulePath = (Get-Command -Name Start-EVXWatcher -CommandType Cmdlet).Module.Path
        $Watcher = Start-EVXWatcher -Name ('PSEventViewer.MutableVariable.' + [Guid]::NewGuid().ToString('N')) -MachineName $env:COMPUTERNAME -LogName Application -EventId 1 -Action {}
        try {
            Set-Variable -Name PSEventViewer_WatcherOwnerId -Scope Global -Value ([Guid]::NewGuid()) -Force
            Remove-Module PSEventViewer -Force

            $Watcher.EndTime | Should -Not -BeNullOrEmpty
        } finally {
            Remove-Variable -Name PSEventViewer_WatcherOwnerId -Scope Global -Force -ErrorAction SilentlyContinue
            if ($Watcher -and -not $Watcher.EndTime) {
                [EventViewerX.WatcherManager]::StopWatcher($Watcher.Id) | Out-Null
            }
            Import-Module -Name $ModulePath -Force
        }
    }

    It 'does not borrow a newer owner when an unused module instance is removed' {
        $RegistryType = [PSEventViewer.CmdletStartEVXWatcher].Assembly.GetType('PSEventViewer.PowerShellWatcherRegistry', $true)
        $Flags = [Reflection.BindingFlags]'Static,NonPublic'
        $Begin = $RegistryType.GetMethod('BeginModuleInstance', $Flags)
        $Register = $RegistryType.GetMethod('Register', $Flags)
        $End = $RegistryType.GetMethod('EndModuleInstance', $Flags)
        $StopAndRemove = $RegistryType.GetMethod('StopAndRemoveOwner', $Flags)
        $RunspaceId = [Guid]::NewGuid()
        $ActiveOwner = $Begin.Invoke($null, @($RunspaceId))
        $Ids = [Collections.Generic.List[int]]::new()
        $Ids.Add(1)
        $NamedEvents = [Collections.Generic.List[EventViewerX.EventType]]::new()
        $Action = [Action[EventViewerX.EventObject]] { param($EventObject) }
        $Watcher = [EventViewerX.WatcherManager]::StartWatcher(
            ('PSEventViewer.OwnerIsolation.' + [Guid]::NewGuid().ToString('N')),
            $env:COMPUTERNAME,
            'Application',
            $Ids,
            $NamedEvents,
            $Action,
            $false,
            $false,
            0,
            $null)
        try {
            $null = $Register.Invoke($null, @($ActiveOwner, $Watcher.Id))
            $null = $End.Invoke($null, @($RunspaceId, $null))

            $Watcher.EndTime | Should -BeNullOrEmpty
        } finally {
            $null = $StopAndRemove.Invoke($null, @($RunspaceId, $ActiveOwner))
            if (-not $Watcher.EndTime) {
                [EventViewerX.WatcherManager]::StopWatcher($Watcher.Id) | Out-Null
            }
        }
    }
}
