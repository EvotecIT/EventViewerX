Describe 'New-EVXFilter' {
    It 'returns a reusable EventFilter by default' {
        $Filter = New-EVXFilter `
            -EventId 4625 `
            -ProviderName 'Microsoft-Windows-Security-Auditing' `
            -TimePeriod PastDay

        $Filter.GetType().FullName | Should -Be 'EventViewerX.EventFilter'
        $Filter.EventIds | Should -Be 4625
        $Filter.ProviderNames | Should -Be 'Microsoft-Windows-Security-Auditing'
        $Filter.StartTime | Should -BeGreaterThan ([datetime]::Now.AddDays(-2))
    }

    It 'keeps Get-EVXFilter as a migration alias to the typed builder' {
        (Get-Alias Get-EVXFilter).ResolvedCommandName | Should -Be 'New-EVXFilter'
        (Get-EVXFilter -EventId 7040).GetType().FullName |
            Should -Be 'EventViewerX.EventFilter'
    }

    It 'compiles one native XPath expression for Get-WinEvent interop' {
        $XPath = New-EVXFilter `
            -EventId 1, 2 `
            -NamedDataFilter @{ FieldName = 'Value1' } `
            -AsXPath

        $XPath | Should -Be "(*[System[(EventID=1) or (EventID=2)]]) and (*[EventData[Data[@Name='FieldName'] = 'Value1']])"
    }

    It 'requires QueryList XML for named-data exclusions' {
        {
            New-EVXFilter `
                -NamedDataExcludeFilter @{ FieldName = 'Value1' } `
                -AsXPath
        } | Should -Throw '*QueryList XML*'
    }

    It 'emits channel QueryList XML with native suppression' {
        [xml] $FilterXml = New-EVXFilter `
            -LogName System `
            -EventId 7040 `
            -NamedDataExcludeFilter @{ param4 = 'BITS' }

        $FilterXml.QueryList.Query.Path | Should -Be 'System'
        $FilterXml.QueryList.Query.Select.'#text' | Should -Be '*[System[EventID=7040]]'
        $FilterXml.QueryList.Query.Suppress.'#text' |
            Should -Be "*[EventData[Data[@Name='param4'] = 'BITS']]"
    }

    It 'emits file QueryList XML without a channel dependency' {
        $FilePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        [xml] $FilterXml = New-EVXFilter `
            -Path $FilePath `
            -EventId 7040 `
            -NamedDataExcludeFilter @{ param4 = 'BITS' }

        $FilterXml.QueryList.Query.Path | Should -BeLike 'file://*NamedFilterExamples.evtx'
        $FilterXml.QueryList.Query.Select.Path | Should -BeLike 'file://*NamedFilterExamples.evtx'
    }

    It 'supports keyword masks and exclusions through the same compiler' {
        (New-EVXFilter -Keywords 1125899906842624 -AsXPath) |
            Should -Be '*[System[band(Keywords,1125899906842624)]]'
        (New-EVXFilter -ExcludeEventId 1, 2 -AsXPath) |
            Should -Be '*[System[(EventID!=1) and (EventID!=2)]]'
    }

    It 'reuses the typed filter directly in Get-EVXEvent' {
        $FilePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $Filter = New-EVXFilter `
            -EventId 7040 `
            -NamedDataFilter @{ param4 = 'BITS' }

        $Events = @(Get-EVXEvent `
                -Path $FilePath `
                -Filter $Filter `
                -Oldest `
                -ReadMode StructuredData)

        $Events | Should -Not -BeNullOrEmpty
        $Events[0].Data['param4'] | Should -Be 'BITS'
    }

    It 'exposes discoverable domain fields for built-in and composite types' {
        $Leaf = New-EVXFilter -Type ADUserLogonFailed
        $Composite = New-EVXFilter -Type ActiveDirectoryAuthentication

        $Leaf.GetType().FullName | Should -Be 'PSEventViewer.PowerShellEventPredicateBuilder'
        $Leaf.Fields.PSObject.Properties.Name | Should -Contain 'Who'
        $Leaf.Fields.PSObject.Properties.Name | Should -Contain 'IpAddress'
        $Composite.Fields.PSObject.Properties.Name | Should -Contain 'Who'
        $Composite.Fields.PSObject.Properties.Name | Should -Contain 'EventId'
    }

    It 'builds and explains one shared typed predicate without querying a source' {
        $Filter = New-EVXFilter -Type ADUserLogonFailed
        $Filter.AllOf(
            $Filter.Fields.EventId.In(4624, 4625),
            $Filter.Fields.Who.Contains('svc-')
        )

        $Plan = Get-EVXEvent -Filter $Filter -Explain

        $Plan.HasNativeFilter | Should -BeTrue
        $Plan.IsFullyNative | Should -BeFalse
        @($Plan.Steps | ForEach-Object { $_.Stage.ToString() }) | Should -Contain 'Native'
        @($Plan.Steps | ForEach-Object { $_.Stage.ToString() }) | Should -Contain 'Managed'
    }

    It 'retains the agreed discoverable filter for direct reuse through Get-EVXEvent -Filter' {
        $Filter = New-EVXFilter -Type ADUserLogonFailed
        $ConfigurationOutput = @($Filter.AllOf(
            $Filter.Fields.Who.In('EVOTEC\Alice', 'EVOTEC\Bob'),
            $Filter.Fields.IPAddress.MatchesSubnet('10.0.0.0/8')
        ))

        $ConfigurationOutput | Should -BeNullOrEmpty
        $Filter.Predicate.Kind.ToString() | Should -Be 'All'
        $Plan = Get-EVXEvent -Filter $Filter -TimePeriod Last7Days -Explain
        $Plan.ManagedPredicate | Should -Not -BeNullOrEmpty
        (Get-Command Get-EVXEvent).ParameterSets.Name | Should -Contain 'TypedFilter'
    }

    It 'reuses a typed filter for offline EVTX paths' {
        $FilePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $DefinitionPath = Join-Path $TestDrive 'offline-service-change.json'
        @{
            Name = 'OfflineServiceStartTypeChange'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        $Filter = New-EVXFilter -Definition $DefinitionPath
        $Filter.Use($Filter.Fields.ServiceName.Equal('BITS'))

        $Plan = Get-EVXEvent -Filter $Filter -Path $FilePath -Explain
        $Events = @(Get-EVXEvent -Filter $Filter -Path $FilePath -Oldest)
        $CheckpointPath = Join-Path $TestDrive 'typed-offline-checkpoint.json'
        $CheckpointEvents = @(Get-EVXEvent `
                -Filter $Filter `
                -Path $FilePath `
                -RecordIdFile $CheckpointPath `
                -Oldest)
        $ReplayedEvents = @(Get-EVXEvent `
                -Filter $Filter `
                -Path $FilePath `
                -RecordIdFile $CheckpointPath `
                -Oldest)

        $Plan.ManagedPredicate | Should -Not -BeNullOrEmpty
        $Events | Should -Not -BeNullOrEmpty
        @($Events.ServiceName | Sort-Object -Unique) | Should -Be @('BITS')
        $CheckpointEvents.Count | Should -Be $Events.Count
        $ReplayedEvents | Should -BeNullOrEmpty
        Test-Path -LiteralPath $CheckpointPath | Should -BeTrue
        ((Get-Command Get-EVXEvent).ParameterSets |
                Where-Object Name -EQ 'Path').Parameters.Name |
            Should -Contain 'Filter'
    }

    It 'rejects native-only Path selectors instead of silently ignoring them for a typed filter' {
        $FilePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $Filter = New-EVXFilter -Type ADUserLogonFailed

        {
            Get-EVXEvent -Filter $Filter -Path $FilePath -EventId 7040
        } | Should -Throw '*typed Filter with Path*-EventId*'
    }

    It 'does not replace explicit Channel or Provider sources with a retained typed source' {
        $Filter = New-EVXFilter -Type ADUserLogonFailed

        {
            Get-EVXEvent -LogName System -Filter $Filter -MaxEvents 1
        } | Should -Throw '*Native Channel, Path, and Provider queries require an EventFilter*'
        {
            Get-EVXEvent -ProviderName 'Service Control Manager' -Filter $Filter -MaxEvents 1
        } | Should -Throw '*Native Channel, Path, and Provider queries require an EventFilter*'
    }

    It 'describes typed fields and explains an inline reusable filter without reading events' {
        $Description = Get-EVXEvent -Type ADUserLogonFailed -Describe
        $Plan = New-EVXFilter -Type ADUserLogonFailed -Where {
            $_.Who -like 'EVOTEC\*'
        } -Explain

        $Description.Name | Should -Be 'ADUserLogonFailed'
        $Description.Fields.Name | Should -Contain 'Who'
        $Description.Fields.Name | Should -Contain 'IpAddress'
        $Plan.ManagedPredicate | Should -Not -BeNullOrEmpty
    }

    It 'rejects misspelled typed fields before any event query runs' {
        {
            New-EVXFilter -Type ADUserLogonFailed -Where {
                $_.DefinitelyMissing -eq 'value'
            }
        } | Should -Throw '*Available fields*'
    }

    It 'rejects invalid typed literals before any event query runs' {
        {
            New-EVXFilter -Type ADUserLogonFailed -Where {
                $_.EventId -eq 'not-a-number'
            }
        } | Should -Throw '*EventId*Int32*'
    }

    It 'accepts a restricted PowerShell expression but never executes arbitrary script' {
        $Plan = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -in @('EVOTEC\alice', 'EVOTEC\bob') -and
                $_.IpAddress -like '10.*'
        } -Explain

        $Plan.Steps.Count | Should -BeGreaterOrEqual 2
        {
            Get-EVXEvent -Type ADUserLogonFailed -Where {
                Get-Process
            } -Explain
        } | Should -Throw '*commands or pipelines*'
    }

    It 'accepts singleton explicit arrays in restricted PowerShell expressions' {
        $Plan = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -in @('EVOTEC\alice')
        } -Explain

        $Plan.ManagedPredicate.Operator.ToString() | Should -Be 'In'
        @($Plan.ManagedPredicate.Values).Count | Should -Be 1
        $Plan.ManagedPredicate.Values[0] | Should -Be 'EVOTEC\alice'
    }

    It 'accepts literal statement arrays and rejects empty membership without executing them' {
        $Plan = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -in @('EVOTEC\alice'; 'EVOTEC\bob')
        } -Explain

        @($Plan.ManagedPredicate.Values) | Should -Be @('EVOTEC\alice', 'EVOTEC\bob')
        {
            Get-EVXEvent -Type ADUserLogonFailed -Where {
                $_.Who -in @()
            } -Explain
        } | Should -Throw '*requires at least one value*'
    }

    It 'accepts the safe PowerShell exclamation alias for predicate negation' {
        $Plan = Get-EVXEvent -Type ADUserLogonFailed -Where {
            !($_.Who -eq 'EVOTEC\alice')
        } -Explain

        $Plan.ManagedPredicate.Kind.ToString() | Should -Be 'Not'
    }

    It 'accepts signed numeric literals without evaluating arbitrary expressions' {
        $DefinitionPath = Join-Path $TestDrive 'signed-number-definition.json'
        @{
            Name = 'SignedNumberDefinition'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(1)
                })
            Fields = @(@{
                    Name = 'Attempts'
                    ValueKind = 'Int32'
                    Source = 'Data'
                    SourceName = 'Attempts'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $Negative = New-EVXFilter -Definition $DefinitionPath -Where {
            $_.Attempts -eq -1
        } -Explain
        $Positive = New-EVXFilter -Definition $DefinitionPath -Where {
            $_.Attempts -eq +1
        } -Explain

        $Negative.ManagedPredicate.Values[0] | Should -Be '-1'
        $Positive.ManagedPredicate.Values[0] | Should -Be '1'
        {
            New-EVXFilter -Definition $DefinitionPath -Where {
                $_.Attempts -eq -(Get-Random)
            } -Explain
        } | Should -Throw '*numeric literal*'
    }

    It 'preserves PowerShell inequality truthiness for collection fields' {
        $Plan = Get-EVXEvent -Type ADUserPrivilegeUse -Where {
            $_.Privileges -ne 'SeDebugPrivilege'
        } -Explain
        $Mixed = [Collections.Generic.Dictionary[string, object]]::new()
        $Mixed['Privileges'] = [string[]] @('SeDebugPrivilege', 'SeBackupPrivilege')
        $Equal = [Collections.Generic.Dictionary[string, object]]::new()
        $Equal['Privileges'] = [string[]] @('SeDebugPrivilege', 'SeDebugPrivilege')

        [EventViewerX.EventPredicateEvaluator]::Matches($Plan.ManagedPredicate, $Mixed) |
            Should -BeTrue
        [EventViewerX.EventPredicateEvaluator]::Matches($Plan.ManagedPredicate, $Equal) |
            Should -BeFalse
    }

    It 'exposes record selectors and durable checkpoints on reusable typed filters' {
        $Parameters = ((Get-Command Get-EVXEvent).ParameterSets |
                Where-Object Name -EQ 'TypedFilter').Parameters.Name

        $Parameters | Should -Contain 'EventRecordId'
        $Parameters | Should -Contain 'RecordIdFile'
        $Parameters | Should -Contain 'RecordIdKey'
    }

    It 'preserves negative PowerShell operators as exact predicate negation' {
        $Plan = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -notlike 'svc-*' -and $_.Who -notmatch '^test'
        } -Explain

        $Plan.ManagedPredicate.Kind.ToString() | Should -Be 'All'
        @($Plan.ManagedPredicate.Children | ForEach-Object { $_.Kind.ToString() }) |
            Should -Be @('Not', 'Not')
    }

    It 'preserves PowerShell case-sensitive and case-insensitive comparison forms' {
        $Insensitive = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -eq 'EVOTEC\ALICE'
        } -Explain
        $Sensitive = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -ceq 'EVOTEC\ALICE'
        } -Explain

        $Insensitive.ManagedPredicate.IgnoreCase | Should -BeTrue
        $Sensitive.ManagedPredicate.IgnoreCase | Should -BeFalse
    }

    It 'accepts reversed PowerShell membership for collection fields' {
        $Included = Get-EVXEvent -Type ADUserPrivilegeUse -Where {
            'SeDebugPrivilege' -in $_.Privileges
        } -Explain
        $Excluded = Get-EVXEvent -Type ADUserPrivilegeUse -Where {
            'SeDebugPrivilege' -notin $_.Privileges
        } -Explain

        $Included.ManagedPredicate.Operator.ToString() | Should -Be 'In'
        $Excluded.ManagedPredicate.Operator.ToString() | Should -Be 'NotIn'
    }

    It 'rejects a collection literal on the left of reversed membership' {
        {
            Get-EVXEvent -Type ADUserPrivilegeUse -Where {
                @('SeDebugPrivilege', 'SeBackupPrivilege') -in $_.Privileges
            } -Explain
        } | Should -Throw '*Field-right*-in/-notin*one scalar value*'
    }

    It 'rejects field-left membership for collection fields' {
        {
            Get-EVXEvent -Type ADUserPrivilegeUse -Where {
                $_.Privileges -in @('SeDebugPrivilege')
            } -Explain
        } | Should -Throw '*Field-left*-in/-notin*collection*'
        {
            New-EVXFilter -Type ADUserPrivilegeUse -Where {
                $_.Privileges -notin @('SeDebugPrivilege')
            } -Explain
        } | Should -Throw '*Field-left*-in/-notin*collection*'
    }

    It 'preserves scalar PowerShell containment as whole-value equality' {
        $Included = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -contains 'EVOTEC\alice'
        } -Explain
        $Excluded = Get-EVXEvent -Type ADUserLogonFailed -Where {
            $_.Who -notcontains 'alice'
        } -Explain

        $Included.ManagedPredicate.Operator.ToString() | Should -Be 'Equal'
        $Excluded.ManagedPredicate.Kind.ToString() | Should -Be 'Not'
        $Excluded.ManagedPredicate.Children[0].Operator.ToString() | Should -Be 'Equal'
    }

    It 'rejects reversed containment whose collection semantics cannot be preserved' {
        {
            Get-EVXEvent -Type ADUserPrivilegeUse -Where {
                'SeDebugPrivilege' -contains $_.Privileges
            } -Explain
        } | Should -Throw '*Use*value*-in*$_.Field*'
    }

    It 'rejects reversed equality whose collection semantics cannot be preserved' {
        {
            Get-EVXEvent -Type ADUserPrivilegeUse -Where {
                'SeDebugPrivilege' -eq $_.Privileges
            } -Explain
        } | Should -Throw '*Field-right*-eq/-ne*collection*'
        {
            New-EVXFilter -Type ADUserPrivilegeUse -Where {
                'SeDebugPrivilege' -cne $_.Privileges
            } -Explain
        } | Should -Throw '*Field-right*-eq/-ne*collection*'
    }

    It 'rejects PowerShell null equality whose collection emission semantics cannot be preserved' {
        {
            Get-EVXEvent -Type ADUserPrivilegeUse -Where {
                $_.Privileges -eq $null
            } -Explain
        } | Should -Throw '*against $null emits collection elements*IsNull*'
        {
            New-EVXFilter -Type ADUserPrivilegeUse -Where {
                $_.Privileges -cne $null
            } -Explain
        } | Should -Throw '*against $null emits collection elements*IsNotNull*'
    }

    It 'preserves reversed equality for scalar fields' {
        $Plan = Get-EVXEvent -Type ADUserLogonFailed -Where {
            'EVOTEC\Alice' -eq $_.Who
        } -Explain

        $Plan.ManagedPredicate.Field | Should -Be 'Who'
        $Plan.ManagedPredicate.Operator.ToString() | Should -Be 'Equal'
        $Plan.ManagedPredicate.Values | Should -Be 'EVOTEC\Alice'
    }

    It 'describes the expanded domain fields of composite event types' {
        $Description = Get-EVXEvent -Type ActiveDirectoryAuthentication -Describe
        $FieldNames = @($Description.Fields.Name)

        $Description.IsComposite | Should -BeTrue
        $FieldNames | Should -Contain 'Who'
        $FieldNames | Should -Contain 'IpAddress'
    }

    It 'exposes native event levels through every typed filter builder' {
        $Filter = New-EVXFilter -Type ADUserLogonFailed
        $Filter.Fields.PSObject.Properties.Name | Should -Contain 'Level'
        $Filter.Use($Filter.Fields.Level.Equal([EventViewerX.Level]::Error)) | Out-Null

        $Plan = Get-EVXEvent -Filter $Filter -Explain

        $Plan.NativeFilter.Levels | Should -Be 2
        $Plan.ManagedPredicate.Field | Should -Be 'Level'
        $Plan.Steps.Expression | Should -Contain 'Exact predicate verification'
    }

    It 'isolates default checkpoints for different inline typed predicates' {
        $FilePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $DefinitionPath = Join-Path $TestDrive 'inline-checkpoint-definition.json'
        $CheckpointPath = Join-Path $TestDrive 'inline-checkpoints.json'
        @{
            Name = 'InlineCheckpointServiceChange'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $Bits = @(Get-EVXEvent `
                -Definition $DefinitionPath `
                -Path $FilePath `
                -Where { $_.ServiceName -eq 'BITS' } `
                -RecordIdFile $CheckpointPath `
                -Oldest)
        $TrustedInstaller = @(Get-EVXEvent `
                -Definition $DefinitionPath `
                -Path $FilePath `
                -Where { $_.ServiceName -eq 'TrustedInstaller' } `
                -RecordIdFile $CheckpointPath `
                -Oldest)

        $Bits | Should -Not -BeNullOrEmpty
        $TrustedInstaller | Should -Not -BeNullOrEmpty
        @($Bits.ServiceName | Sort-Object -Unique) | Should -Be @('BITS')
        @($TrustedInstaller.ServiceName | Sort-Object -Unique) |
            Should -Be @('TrustedInstaller')
    }

    It 'explains custom aliases from definition metadata rather than native names' {
        $DefinitionPath = Join-Path $TestDrive 'custom-explain.json'
        @{
            Name = 'CustomExplain'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                })
            Fields = @(@{
                    Name = 'ServiceLabel'
                    Aliases = @('ProviderName')
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $Filter = New-EVXFilter -Definition $DefinitionPath
        $Filter.Use($Filter.Fields.ProviderName.Equal('BITS')) | Out-Null
        $Plan = Get-EVXEvent -Filter $Filter -Explain

        $Plan.NativeFilter | Should -BeNullOrEmpty
        $Plan.ManagedPredicate.Field | Should -Be 'ServiceLabel'
    }
}
