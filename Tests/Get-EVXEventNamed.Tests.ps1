Describe 'Get-EVXEvent - Type' {
    It 'uses Type as the canonical parameter and preserves migration aliases' {
        $Parameter = (Get-Command Get-EVXEvent).Parameters['Type']
        $Parameter.Aliases | Should -Contain 'NamedEvent'
        $Parameter.Aliases | Should -Contain 'NamedEvents'
    }

    It 'exposes opt-in DNS enrichment only on the Type parameter set' {
        $Command = Get-Command Get-EVXEvent
        $Command.Parameters.Keys | Should -Contain 'ResolveDns'
        $Command.Parameters.Keys | Should -Contain 'DnsTimeoutMs'
        $Command.Parameters.Keys | Should -Contain 'DnsMaxConcurrency'

        $ResolveDnsSets = @($Command.Parameters['ResolveDns'].ParameterSets.Keys)
        $DnsTimeoutSets = @($Command.Parameters['DnsTimeoutMs'].ParameterSets.Keys)
        $DnsConcurrencySets = @($Command.Parameters['DnsMaxConcurrency'].ParameterSets.Keys)
        $ResolveDnsSets | Should -Be @('Type')
        $DnsTimeoutSets | Should -Be @('Type')
        $DnsConcurrencySets | Should -Be @('Type')
    }

    It 'exposes native remote, failure, bookmark, and Int64 scan controls without no-op filters' {
        $Command = Get-Command Get-EVXEvent
        $NamedSet = $Command.ParameterSets |
            Where-Object Name -EQ 'Type'

        $NamedSet.Parameters.Name | Should -Contain 'Credential'
        $NamedSet.Parameters.Name | Should -Contain 'Authentication'
        $NamedSet.Parameters.Name | Should -Contain 'ContinueOnError'
        $NamedSet.Parameters.Name | Should -Contain 'SessionTimeoutMs'
        $NamedSet.Parameters.Name | Should -Contain 'BufferCapacity'
        $NamedSet.Parameters.Name | Should -Contain 'IncludeBookmark'
        $NamedSet.Parameters.Name | Should -Contain 'Path'
        $NamedSet.Parameters.Name | Should -Not -Contain 'NamedDataFilter'
        $Command.Parameters.MaxEventsScanned.ParameterType |
            Should -Be ([long])
    }

    It 'accepts an offline container override while the type keeps event semantics' {
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        {
            $null = @(Get-EVXEvent -Type OSStartup -Path $Fixture -MaxEvents 1 -ErrorAction Stop)
        } | Should -Not -Throw
    }

    It 'rejects credentials when any event-type target is local' {
        $SecurePassword = ConvertTo-SecureString 'not-used' -AsPlainText -Force
        $Credential = [pscredential]::new('reader', $SecurePassword)

        {
            Get-EVXEvent `
                -Type OSStartup `
                -Credential $Credential `
                -ErrorAction Stop
        } | Should -Throw '*every event-type target is a remote computer*'
        {
            Get-EVXEvent `
                -Type OSStartup `
                -MachineName $env:COMPUTERNAME, 'remote.contoso.test' `
                -Credential $Credential `
                -ErrorAction Stop
        } | Should -Throw '*every event-type target is a remote computer*'
    }


    It 'Returns ADUserLogon events when available' -Tag 'RequiresEvents' {
        $events = Get-EVXEvent -Type ADUserLogon -MaxEvents 1 -ErrorAction SilentlyContinue
        if ($events) {
            $events.Count | Should -BeGreaterThan 0
        } else {
            Write-Warning 'No ADUserLogon events found on this system.'
        }
    }

    It 'expands structured payload fields on event-type projections' {
        $Plain = Get-EVXEvent -Type OSStartup -MaxEvents 1 -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $Plain) {
            Set-ItResult -Skipped -Because 'No OSStartup event was available.'
            return
        }

        $Plain.SourceEvent.ReadMode | Should -Be ([EventViewerX.EventReadMode]::StructuredDataAndMessage)
        $ExistingProperties = @($Plain.PSObject.Properties.Name)
        $PayloadKey = @($Plain.SourceEvent.Data.Keys | Where-Object { $_ -notin $ExistingProperties } | Select-Object -First 1)
        if (-not $PayloadKey) {
            Set-ItResult -Skipped -Because 'The OSStartup payload had no distinct field to expand.'
            return
        }

        $Expanded = Get-EVXEvent -Type OSStartup -MaxEvents 1 -ReadMode Full -ExpandData -ErrorAction Stop | Select-Object -First 1

        $Expanded.PSObject.Properties.Name | Should -Contain $PayloadKey[0]
        $Expanded.PSObject.BaseObject | Should -BeOfType $Plain.GetType()
    }

    It 'applies MessageRegex before the global event-type result limit' {
        $Types = @('OSStartup', 'OSShutdown', 'OSStartupSecurity')
        $Expected = @(Get-EVXEvent -Type $Types -MaxEvents 5 -ErrorAction SilentlyContinue)
        if ($Expected.Count -lt 2 -or @($Expected.SourceEvent.ContainerLog | Sort-Object -Unique).Count -lt 2) {
            Set-ItResult -Skipped -Because 'Two event-type logs were not available for a global-order comparison.'
            return
        }

        $Actual = @(Get-EVXEvent -Type $Types -MessageRegex '(?s).*' -MaxEvents 5 -ErrorAction Stop)
        $ExpectedKeys = @($Expected | ForEach-Object { '{0}|{1}|{2}' -f $_.SourceEvent.QueriedMachine, $_.SourceEvent.ContainerLog, $_.SourceEvent.RecordId })
        $ActualKeys = @($Actual | ForEach-Object { '{0}|{1}|{2}' -f $_.SourceEvent.QueriedMachine, $_.SourceEvent.ContainerLog, $_.SourceEvent.RecordId })

        ($ActualKeys -join "`n") | Should -Be ($ExpectedKeys -join "`n")
    }

    It 'reports an isolated remote event-type failure instead of silently returning partial results' {
        $Errors = @()
        $null = @(
            Get-EVXEvent `
                -Type OSStartup `
                -MachineName $env:COMPUTERNAME, '192.0.2.1' `
                -SessionTimeoutMs 500 `
                -MaxEvents 0 `
                -ErrorAction SilentlyContinue `
                -ErrorVariable +Errors
        )

        @($Errors.FullyQualifiedErrorId) |
            Should -Contain 'EVXEventTypeTargetFailed,PSEventViewer.CmdletGetEVXEvent'
    }
}

Describe 'Get-EVXEvent - Definition' {
    It 'exposes a composable custom-definition parameter set without adding a cmdlet' {
        $Set = (Get-Command Get-EVXEvent).ParameterSets |
            Where-Object Name -EQ 'Definition'

        $Set | Should -Not -BeNullOrEmpty
        $Set.Parameters.Name | Should -Contain 'Definition'
        $Set.Parameters.Name | Should -Contain 'Path'
        $Set.Parameters.Name | Should -Contain 'Collector'
        $Set.Parameters.Name | Should -Contain 'RecordIdFile'
        $Set.Parameters.Name | Should -Not -Contain 'LogName'
        $Set.Parameters.Name | Should -Not -Contain 'ResolveDns'
    }

    It 'projects custom fields from an offline EVTX file' {
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
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'

        $Rows = @(Get-EVXEvent -Definition $DefinitionPath -Path $Fixture -MaxEvents 2 -ErrorAction Stop)

        $Rows.Count | Should -Be 2
        @($Rows.TypeName | Sort-Object -Unique) | Should -Be @('ServiceStartTypeChange')
        $Rows[0].PSObject.Properties.Name | Should -Contain 'ServiceName'
        $Rows[0].PSObject.BaseObject | Should -BeOfType ([EventViewerX.CustomEventRecord])
    }
}
