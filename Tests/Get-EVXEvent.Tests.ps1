Describe 'Get-EVXEvent - Basic Test (EVTX sample)' {
    $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')

    $Events = Get-EVXEvent -Path $FilePath -MaxEvents 3

    $PSDefaultParameterValues = @{
        "It:TestCases" = @{ Date = $Date; Date1 = $Date1; Events = $Events }
    }

    It 'Should have GatheredFrom filled in' {
        $Events[0].GatheredFrom | Should -Not -BeNullOrEmpty
    }
    It 'Should have more then 1 event' {
        $Events.Count | Should -BeGreaterOrEqual 1
    }
    It 'Should return an Array' {
        $Events -is [Array] | Should -Be $true
    }
    It 'Should return proper LogName' {
        $Events[0].LogName | Should -Be 'Active Directory Web Services'
    }
    It 'Should default to message projection rather than eager full payload parsing' {
        $Events[0].ReadMode | Should -Be ([EventViewerX.EventReadMode]::Message)
    }
}

Describe 'Get-EVXEvent - RawXml bookmark projection' {
    It 'materializes a bookmark only when explicitly requested' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'Active Directory Web Services.evtx')

        $WithoutBookmark = Get-EVXEvent `
            -Path $FilePath `
            -MaxEvents 1 `
            -ReadMode RawXml
        $WithBookmark = Get-EVXEvent `
            -Path $FilePath `
            -MaxEvents 1 `
            -ReadMode RawXml `
            -IncludeBookmark

        $WithoutBookmark.Bookmark | Should -BeNullOrEmpty
        $WithBookmark.Bookmark | Should -Not -BeNullOrEmpty
        $WithBookmark.XMLData | Should -Not -BeNullOrEmpty
        $WithBookmark.Properties | Should -BeNullOrEmpty
    }
}

Describe 'Get-EVXEvent provider-only binding' {
    It 'does not require LogName when ProviderName is supplied' {
        {
            Get-EVXEvent `
                -ProviderName Microsoft-Windows-Kernel-General `
                -EventId 12 `
                -MaxEvents 1 `
                -ReadMode Metadata
        } | Should -Not -Throw
    }

    It 'uses a structured suppression for provider named-data exclusions' {
        {
            Get-EVXEvent `
                -ProviderName Microsoft-Windows-Kernel-General `
                -EventId 12 `
                -NamedDataExcludeFilter @{
                    FieldThatDoesNotExist = 'ExcludedValue'
                } `
                -MaxEvents 1 `
                -ReadMode Metadata
        } | Should -Not -Throw
    }

    It 'preserves positional LogName binding' {
        $Command = Get-Command Get-EVXEvent
        $Generic = $Command.ParameterSets |
            Where-Object Name -EQ 'Channel'
        $LogName = $Generic.Parameters |
            Where-Object Name -EQ 'LogName'
        $Provider = $Command.ParameterSets |
            Where-Object Name -EQ 'Provider' |
            ForEach-Object Parameters |
            Where-Object Name -EQ 'ProviderName'

        $LogName.Position | Should -Be 0
        $Provider.Position | Should -Be ([int]::MinValue)
    }
}

Describe 'Get-EVXEvent - MaxEvents Test (EVTX sample)' {
    $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')

    $Events = Get-EVXEvent -Path $FilePath -MaxEvents 1

    $PSDefaultParameterValues = @{
        "It:TestCases" = @{ Date = $Date; Date1 = $Date1; Events = $Events }
    }

    It 'Should have GatheredFrom filled in' {
        $Events[0].GatheredFrom | Should -Not -BeNullOrEmpty
        $Events[0].LogName | Should -Be 'Active Directory Web Services'
    }
    It 'Should have exactly 1 event' {
        $Events.Count | Should -BeExactly 1
    }
    It 'Should return proper LogName' {
        $Events[0].LogName | Should -Be 'Active Directory Web Services'
    }
}

Describe 'Get-EVXEvent - MaxEvents on sample file' {
    It 'Should return up to requested events' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $Events = Get-EVXEvent -Path $FilePath -MaxEvents 3
        $Events.Count | Should -BeGreaterThan 0
        $Events.Count | Should -BeLessOrEqual 3
    }
    It 'Should have GatheredFrom set to file path' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $Events = Get-EVXEvent -Path $FilePath -MaxEvents 3
        ($Events | Select-Object -First 1).GatheredFrom | Should -Not -BeNullOrEmpty
    }
}

Describe 'Get-EVXEvent - Read events from path (oldest / newest)' {
    $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')

    $PSDefaultParameterValues = @{
        "It:TestCases" = @{ FilePath = $FilePath; }
    }

    It 'Should read 1 oldest event' {

        $Events = Get-EVXEvent -Path $FilePath -Oldest -MaxEvents 1 #-Verbose
        $Events.Count | Should -Be 1
        $Events[0].Id | Should -Be 1000
        $Events[0].GatheredFrom | Should -Be $FilePath
    }

    It 'Should read 1 newest event' {

        $EventsNewest = Get-EVXEvent -Path $FilePath -MaxEvents 1 -ReadMode StructuredData -ExpandData
        $EventsNewest.Count | Should -Be 1
        $EventsNewest[0].Id | Should -Be 1200
        $EventsNewest[0].GatheredFrom | Should -Be $FilePath

        $EventsNewest[0].NoNameA0 | Should -Be 'GC'
        $EventsNewest[0].NoNameA1 | Should -Be 3268
        $EventsNewest[0].NoNameA2 | Should -Be 3269
    }
}

Describe 'Get-EVXEvent - Read events with NamedDataFilter' {
    $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'NamedFilterExamples.evtx')

    $PSDefaultParameterValues = @{
        "It:TestCases" = @{ FilePath = $FilePath; }
    }

    It 'Using -Path should not fail' {
        Get-EVXEvent -Path $FilePath -Oldest -MaxEvents 1 -ErrorVariable err
        $err | Should -BeNullOrEmpty
    }

    It 'named exclude filter' {
        $ret = @(Get-EVXEvent -Path $FilePath -Id 7040 -NamedDataExcludeFilter @{ param4 = ('BITS', 'TrustedInstaller') } -MaxEvents 1 -ReadMode StructuredData -ExpandData)
        $ret | Should -HaveCount 1
        ( [datetime] $ret.TimeCreated ) | Should -Be ( [datetime] "2019-08-30T06:57:44.037957100Z" )
        $ret.param4 | Should -Be 'NgcCtnrSvc'

    }
    It 'named exclude filter keeps events where the field is absent' {
        $Expected = @(Get-EVXEvent -Path $FilePath -Oldest -ReadMode Metadata)
        $Actual = @(Get-EVXEvent -Path $FilePath -Oldest -NamedDataExcludeFilter @{
                FieldThatDoesNotExistInFixture = 'ExcludedValue'
            } -ReadMode Metadata)

        $Expected | Should -Not -BeNullOrEmpty
        $Actual.RecordId | Should -Be $Expected.RecordId
    }
    It 'named include filter' {
        $ret = @(Get-EVXEvent -Path $FilePath -Id 7040 -NamedDataFilter @{ param4 = ('BITS', 'TrustedInstaller') } -oldest -MaxEvents 1 -ReadMode StructuredData -ExpandData)
        $ret | Should -HaveCount 1
        ( [datetime] $ret.TimeCreated ) | Should -Be ( [datetime] "2019-08-30T06:50:13.213617700Z" )
        $ret.param4 | Should -Be 'BITS'

    }
}

Describe 'Get-EVXEvent - MessageRegex' {
    It 'Supports filtering by message regex' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $events   = Get-EVXEvent -Path $FilePath -MaxEvents 1 -MessageRegex '.*'
        $events.Count | Should -Be 1
    }

    It 'applies the regex before the global cap across EVTX query chunks' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $RealIds = @(1200, 1202, 1004, 1006, 1008, 1400, 1000, 1100)
        $ChunkedIds = [Collections.Generic.List[int]]::new()
        $SyntheticId = 30000
        foreach ($RealId in $RealIds) {
            $ChunkedIds.Add($RealId)
            foreach ($Offset in 1..21) {
                $ChunkedIds.Add($SyntheticId)
                $SyntheticId++
            }
        }

        $Expected = @(Get-EVXEvent -Path $FilePath -EventId $ChunkedIds -MaxEvents 8 -ReadMode Message)
        $Actual = @(Get-EVXEvent -Path $FilePath -EventId $ChunkedIds -MessageRegex '(?s).*' -MaxEvents 8 -ReadMode Message)

        ($Actual.RecordId -join ',') | Should -Be ($Expected.RecordId -join ',')
    }
}

Describe 'Get-EVXEvent - Parameter validation' {
    It 'uses an Int64 MaxEvents contract like Get-WinEvent' {
        (Get-Command Get-EVXEvent).Parameters.MaxEvents.ParameterType |
            Should -Be ([long])
    }

    It 'Fails when NumberOfThreads is less than 1' {
        { Get-EVXEvent -LogName 'Application' -NumberOfThreads 0 } | Should -Throw
    }

    It 'Fails when NumberOfThreads exceeds the reusable concurrency bound' {
        { Get-EVXEvent -LogName 'Application' -NumberOfThreads 65 } | Should -Throw
    }

    It 'uses one MaxConcurrency contract while preserving NumberOfThreads as an alias' {
        $Parameter = (Get-Command Get-EVXEvent).Parameters['MaxConcurrency']

        $Parameter | Should -Not -BeNullOrEmpty
        $Parameter.Aliases | Should -Contain 'NumberOfThreads'
        $Parameter.ParameterSets.Keys | Should -Contain 'Channel'
        $Parameter.ParameterSets.Keys | Should -Contain 'Type'
        $Parameter.ParameterSets.Keys | Should -Contain 'Definition'
        $Parameter.ParameterSets.Keys | Should -Contain 'Path'
        $Parameter.ParameterSets.Keys | Should -Contain 'Hashtable'
        $Parameter.ParameterSets.Keys | Should -Contain 'Xml'
        $Parameter.ParameterSets.Keys | Should -Contain 'Provider'
    }

    It 'applies DisableParallel to offline native queries' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')

        $Event = Get-EVXEvent -Path $FilePath -MaxEvents 1 -MaxConcurrency 4 -DisableParallel

        $Event | Should -HaveCount 1
    }

    It 'exposes event-type source and projection controls' {
        $NamedSet = (Get-Command Get-EVXEvent).ParameterSets |
            Where-Object Name -EQ 'Type'

        $NamedSet.Parameters.Name | Should -Contain 'Path'
        $NamedSet.Parameters.Name | Should -Contain 'Collector'
        $NamedSet.Parameters.Name | Should -Not -Contain 'LogName'
        $NamedSet.Parameters.Name | Should -Not -Contain 'EventId'
        $NamedSet.Parameters.Name | Should -Contain 'Oldest'
        $NamedSet.Parameters.Name | Should -Contain 'ReadMode'
        $NamedSet.Parameters.Name | Should -Contain 'MessageCulture'
    }
}

Describe 'Get-EVXEvent - Positional EventId' {
    It 'Allows positional EventId without ambiguity' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $events = Get-EVXEvent -Path $FilePath -Id 1200 -MaxEvents 1
        $events | Should -Not -BeNullOrEmpty
        $events[0].ID | Should -Be 1200
    }
}

Describe 'Get-EVXEvent - EVTX record filtering' {
    It 'forwards EventRecordId to the file query engine' {
        $FilePath = [System.IO.Path]::Combine($PSScriptRoot, 'Logs', 'Active Directory Web Services.evtx')
        $Latest = @(Get-EVXEvent -Path $FilePath -MaxEvents 1 -ReadMode Metadata)[0]

        $Matching = @(Get-EVXEvent -Path $FilePath -EventRecordId $Latest.RecordId -ReadMode Metadata)
        $Missing = @(Get-EVXEvent -Path $FilePath -EventRecordId ($Latest.RecordId + 1000000) -ReadMode Metadata)

        $Matching | Should -HaveCount 1
        $Matching[0].RecordId | Should -Be $Latest.RecordId
        $Missing | Should -HaveCount 0
    }
}

Describe 'Get-EVXEvent - Get-WinEvent compatible native filters' {
    It 'preserves case whitespace and empty FilterHashtable data literals' {
        $Assembly = (Get-Command Get-EVXEvent).ImplementingType.Assembly
        $Adapter = $Assembly.GetType(
            'PSEventViewer.PowerShellEventFilterAdapter',
            $true)
        $BindFilter = $Adapter.GetMethod(
            'BindFilter',
            [System.Reflection.BindingFlags]'NonPublic, Static')
        $Filter = $BindFilter.Invoke(
            $null,
            (, @{
                Path = 'unused.evtx'
                Data = @(' value ', 'VALUE', 'value', '')
                CustomField = @(' Exact ', 'EXACT', 'exact', '')
            }))

        @($Filter.Data) |
            Should -Be @(' value ', 'VALUE', 'value', '')
        @($Filter.NamedData['CustomField']) |
            Should -Be @(' Exact ', 'EXACT', 'exact', '')
    }

    It 'expands provider wildcards and chunks past the native 22-expression limit' {
        $event = Get-EVXEvent `
            -FilterHashtable @{
                LogName = 'System'
                ProviderName = 'Microsoft-Windows-Kernel-*'
            } `
            -MaxEvents 1 `
            -ReadMode Metadata

        $event | Should -Not -BeNullOrEmpty
        $event.ProviderName | Should -Match '^Microsoft-Windows-Kernel-'
    }

    It 'accepts Oldest with a FilterHashtable query' {
        $events = @(Get-EVXEvent -FilterHashtable @{ LogName = 'System' } -Oldest -MaxEvents 2 -ReadMode Metadata)
        $events | Should -HaveCount 2
        $events[0].RecordId | Should -BeLessThan $events[1].RecordId
    }

    It 'partitions SuppressHashFilter values beyond the native XPath limit' {
        $events = @(
            Get-EVXEvent `
                -FilterHashtable @{
                    LogName = 'System'
                    SuppressHashFilter = @{
                        Id = 1..60
                    }
                } `
                -MaxEvents 2 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount 2
        foreach ($event in $events) {
            $event.Id | Should -Not -BeIn (1..60)
        }
    }

    It 'resolves offline SuppressHashFilter provider wildcards from the file' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        $control = @(
            Get-EVXEvent `
                -FilterHashtable @{
                    Path = $FilePath
                    Id = 7040
                } `
                -ReadMode Metadata
        )
        $events = @(
            Get-EVXEvent `
                -FilterHashtable @{
                    Path = $FilePath
                    Id = 7040
                    SuppressHashFilter = @{
                        ProviderName = 'Service*'
                    }
                } `
                -ReadMode Metadata
        )

        $control | Should -Not -BeNullOrEmpty
        $events | Should -HaveCount 0
    }

    It 'bounds offline provider wildcard discovery before the main query' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        {
            Get-EVXEvent `
                -FilterHashtable @{
                    Path = $FilePath
                    SuppressHashFilter = @{
                        ProviderName = 'Service*'
                    }
                } `
                -MaxEventsScanned 1 `
                -ReadMode Metadata
        } | Should -Throw '*provider wildcard discovery*safety limit*'
    }

    It 'accepts several FilterHashtable queries as one ordered union' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')
        $SystemEvent = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Metadata
        $events = @(
            Get-EVXEvent `
                -FilterHashtable @(
                    @{
                        Path = $FilePath
                        Id = 7040
                    },
                    @{
                        LogName = 'System'
                        Id = $SystemEvent.Id
                        StartTime = $SystemEvent.TimeCreated.AddSeconds(-1)
                        EndTime = $SystemEvent.TimeCreated.AddSeconds(1)
                    }
                ) `
                -ReadMode Metadata `
                -Oldest
        )

        $events | Should -Not -BeNullOrEmpty
        $events.GatheredFrom | Should -Contain $FilePath
        $events.ContainerLog | Should -Contain 'System'
        foreach ($source in $events | Group-Object GatheredFrom) {
            $records = @($source.Group.RecordId)
            for ($index = 1; $index -lt $records.Count; $index++) {
                $records[$index] |
                    Should -BeGreaterThan $records[$index - 1]
            }
        }
    }

    It 'scopes offline provider wildcards to their own FilterHashtable source' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')
        $SystemEvent = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Metadata
        $events = @(
            Get-EVXEvent `
                -FilterHashtable @(
                    @{
                        Path = $FilePath
                        Id = 7040
                        ProviderName = 'Service*'
                    },
                    @{
                        LogName = 'System'
                        Id = $SystemEvent.Id
                        StartTime = $SystemEvent.TimeCreated.AddSeconds(-1)
                        EndTime = $SystemEvent.TimeCreated.AddSeconds(1)
                    }
                ) `
                -ReadMode Metadata
        )

        $events.GatheredFrom | Should -Contain $FilePath
        $events.ContainerLog | Should -Contain 'System'
        @(
            $events |
                Where-Object GatheredFrom -EQ $FilePath
        ).ProviderName | Should -BeLike 'Service*'
    }

    It 'accepts LogName and Path together in one FilterHashtable' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        $events = @(
            Get-EVXEvent `
                -FilterHashtable @{
                    LogName = 'System'
                    Path = $FilePath
                    Id = 7040
                } `
                -ReadMode Metadata
        )

        $events.GatheredFrom | Should -Contain $FilePath
        $events.ContainerLog | Should -Contain 'System'
    }

    It 'keeps file sources local in a mixed machine-targeted hashtable batch' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')
        $SystemEvent = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Metadata

        $events = @(
            Get-EVXEvent `
                -FilterHashtable @(
                    @{
                        Path = $FilePath
                        Id = 7040
                    },
                    @{
                        LogName = 'System'
                        Id = $SystemEvent.Id
                    }
                ) `
                -MachineName $env:COMPUTERNAME `
                -ReadMode Metadata
        )

        $events.GatheredFrom | Should -Contain $FilePath
        $events.ContainerLog | Should -Contain 'System'
    }

    It 'deduplicates overlapping FilterHashtable Select expressions natively' {
        $Latest = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Metadata
        $Filter = @{
            LogName = 'System'
            Id = $Latest.Id
        }
        $expected = @(
            Get-EVXEvent `
                -FilterHashtable $Filter `
                -MaxEvents 10 `
                -ReadMode Metadata
        )

        $events = @(
            Get-EVXEvent `
                -FilterHashtable @($Filter, $Filter) `
                -MaxEvents 10 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount $expected.Count
        (@($events.RecordId) -join ',') |
            Should -Be (@($expected.RecordId) -join ',')
        @($events.RecordId | Sort-Object -Unique) |
            Should -HaveCount $events.Count
    }
}

Describe 'Get-EVXEvent - wildcard source parity' {
    It 'expands channel wildcards on the queried machine' {
        $events = @(
            Get-EVXEvent `
                -LogName '*PowerShell*' `
                -MaxEvents 3 `
                -ReadMode Metadata `
                -ContinueOnError `
                -ErrorAction SilentlyContinue
        )

        $events | Should -Not -BeNullOrEmpty
        foreach ($event in $events) {
            $event.ContainerLog | Should -Match 'PowerShell'
        }
    }

    It 'expands offline file path wildcards' {
        $pattern = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterEx?mples.evtx')

        $events = @(
            Get-EVXEvent `
                -Path $pattern `
                -Oldest `
                -MaxEvents 2 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount 2
    }

    It 'matches offline provider wildcards from event metadata' {
        $filePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        $events = @(
            Get-EVXEvent `
                -Path $filePath `
                -ProviderName 'Service*' `
                -Oldest `
                -MaxEvents 3 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount 3
        foreach ($event in $events) {
            $event.ProviderName | Should -BeLike 'Service*'
        }
    }
}

Describe 'Get-EVXEvent - pipeline parity' {
    It 'enforces MaxEvents across channel names from the pipeline' {
        $events = @(
            'System', 'Application' |
                Get-EVXEvent `
                    -MaxEvents 1 `
                    -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events.ContainerLog | Should -Contain 'System'
    }

    It 'accepts PSPath by property name from the pipeline' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        $events = @(
            [pscustomobject] @{ PSPath = $FilePath } |
                Get-EVXEvent `
                    -MaxEvents 1 `
                    -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events[0].GatheredFrom | Should -Be $FilePath
    }

    It 'accepts hashtable queries from the pipeline' {
        $events = @(
            @{ LogName = 'System'; Id = 117 } |
                Get-EVXEvent `
                    -MaxEvents 1 `
                    -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events[0].Id | Should -Be 117
    }

    It 'accepts an XmlDocument query from the pipeline' {
        [xml] $Query = @'
<QueryList>
  <Query Id="0" Path="System">
    <Select Path="System">*[System[EventID=117]]</Select>
  </Query>
</QueryList>
'@

        $events = @(
            $Query |
                Get-EVXEvent `
                    -MaxEvents 1 `
                    -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events[0].Id | Should -Be 117
    }

    It 'infers FilterXml sources from Path attributes instead of XPath text' {
        $Latest = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Metadata
        [xml] $Query = @"
<QueryList>
  <Query Id="0" Path="System">
    <Select Path="System">*[System[EventRecordID=$($Latest.RecordId)] or EventData[Data='file://server/share/item']]</Select>
  </Query>
</QueryList>
"@

        $events = @(
            Get-EVXEvent `
                -FilterXml $Query `
                -MaxEvents 1 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events[0].RecordId | Should -Be $Latest.RecordId
        $events[0].ContainerLog | Should -Be 'System'
    }

    It 'rejects one bookmark across multiple FilterXml file sources' {
        $Fixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $FirstPath = Join-Path $TestDrive 'bookmark-first.evtx'
        $SecondPath = Join-Path $TestDrive 'bookmark-second.evtx'
        Copy-Item -LiteralPath $Fixture -Destination $FirstPath
        Copy-Item -LiteralPath $Fixture -Destination $SecondPath
        $FirstUri = ([Uri]::new([IO.Path]::GetFullPath($FirstPath))).AbsoluteUri
        $SecondUri = ([Uri]::new([IO.Path]::GetFullPath($SecondPath))).AbsoluteUri
        [xml] $Query = @"
<QueryList>
  <Query Id="0" Path="$FirstUri"><Select Path="$FirstUri">*</Select></Query>
  <Query Id="1" Path="$SecondUri"><Select Path="$SecondUri">*</Select></Query>
</QueryList>
"@

        {
            Get-EVXEvent `
                -FilterXml $Query `
                -BookmarkXml '<BookmarkList />' `
                -ReadMode Metadata `
                -ErrorAction Stop
        } | Should -Throw '*exactly one native query source*'
    }

    It 'converts a direct FilterXml string to XmlDocument' {
        $Query = @'
<QueryList>
  <Query Id="0" Path="System">
    <Select Path="System">*[System[EventID=117]]</Select>
  </Query>
</QueryList>
'@

        $events = @(
            Get-EVXEvent `
                -FilterXml $Query `
                -MaxEvents 1 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events[0].Id | Should -Be 117
    }
}

Describe 'Get-EVXEvent - Force parity' {
    It 'exposes Force on wildcard-capable parameter sets' {
        $command = Get-Command Get-EVXEvent

        foreach ($setName in @(
            'Channel',
            'Provider',
            'Hashtable')) {
            $set = $command.ParameterSets |
                Where-Object Name -EQ $setName

            $set | Should -Not -BeNullOrEmpty
            $set.Parameters.Name | Should -Contain 'Force'
        }
    }

    It 'preserves an explicitly named analytic channel beside a wildcard' {
        $AnalyticChannel = [EventViewerX.EventLogCatalog]::GetChannelNames(
            $null,
            [string[]] @('*'),
            $true,
            [System.Threading.CancellationToken]::None
        ) | Where-Object {
            try {
                $Configuration = [System.Diagnostics.Eventing.Reader.EventLogConfiguration]::new($_)
                try {
                    $Configuration.LogType -in @(
                        [System.Diagnostics.Eventing.Reader.EventLogType]::Analytical,
                        [System.Diagnostics.Eventing.Reader.EventLogType]::Debug
                    )
                } finally {
                    $Configuration.Dispose()
                }
            } catch {
                $false
            }
        } | Select-Object -First 1
        if (-not $AnalyticChannel) {
            Set-ItResult -Skipped -Because 'No readable analytic or debug channel is available.'
        }

        $QueryErrors = @()
        Get-EVXEvent `
            -LogName $AnalyticChannel, 'EventViewerX-No-Such-Channel-*' `
            -MaxEvents 1 `
            -ReadMode Metadata `
            -ContinueOnError `
            -ErrorAction SilentlyContinue `
            -ErrorVariable QueryErrors | Out-Null

        ($QueryErrors -join [Environment]::NewLine) |
            Should -Not -Match 'No event channels match'
    }
}

Describe 'Get-EVXEvent - raw XPath wildcard expansion' {
    It 'expands wildcard channel names before applying raw XPath' {
        $events = @(
            Get-EVXEvent `
                -LogName 'Sys*' `
                -FilterXPath '*' `
                -MaxEvents 1 `
                -ReadMode Metadata
        )

        $events | Should -HaveCount 1
        $events[0].ContainerLog | Should -Be 'System'
    }

    It 'rejects raw XPath before partitioning a large offline typed filter' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        {
            Get-EVXEvent `
                -Path $FilePath `
                -FilterXPath '*' `
                -EventId (1..23) `
                -ReadMode Metadata
        } | Should -Throw '*FilterXPath cannot be combined*'
    }

    It 'rejects raw XPath before expanding an offline provider wildcard' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        {
            Get-EVXEvent `
                -Path $FilePath `
                -FilterXPath '*' `
                -ProviderName 'Microsoft-*' `
                -ReadMode Metadata
        } | Should -Throw '*FilterXPath cannot be combined*'
    }
}

Describe 'Get-EVXEvent - bookmark projection' {
    It 'materializes bookmarks only when requested' {
        $FilePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx')

        $without = Get-EVXEvent `
            -Path $FilePath `
            -MaxEvents 1 `
            -ReadMode Message
        $with = Get-EVXEvent `
            -Path $FilePath `
            -MaxEvents 1 `
            -ReadMode Message `
            -IncludeBookmark

        $without.Bookmark | Should -BeNullOrEmpty
        $with.Bookmark | Should -Not -BeNullOrEmpty
    }

    It 'accepts one bookmark after consolidating a partitioned channel filter' {
        $Latest = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Metadata `
            -IncludeBookmark
        if ($null -eq $Latest) {
            Set-ItResult -Skipped -Because 'The System event log is empty.'
            return
        }

        $EventIds = [Collections.Generic.List[int]]::new()
        $EventIds.Add([int] $Latest.Id)
        $Candidate = 30000
        while ($EventIds.Count -lt 23) {
            if ($Candidate -ne $Latest.Id) {
                $EventIds.Add($Candidate)
            }
            $Candidate++
        }
        $Latest.BookmarkXml | Should -Not -BeNullOrEmpty

        $Resumed = @(
            Get-EVXEvent `
                -LogName System `
                -EventId $EventIds `
                -BookmarkXml $Latest.BookmarkXml `
                -BookmarkOffset 0 `
                -MaxEvents 1 `
                -ReadMode Metadata
        )

        $Resumed | Should -HaveCount 1
        $Resumed[0].RecordId | Should -Be $Latest.RecordId
    }
}
