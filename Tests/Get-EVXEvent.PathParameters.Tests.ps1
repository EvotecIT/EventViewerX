Describe 'Get-EVXEvent -Path parameter contract' {
    BeforeAll {
        $PathParameters = (Get-Command Get-EVXEvent).ParameterSets |
            Where-Object Name -EQ 'Path' |
            Select-Object -ExpandProperty Parameters |
            Select-Object -ExpandProperty Name
    }

    It 'exposes every filter forwarded to the EVTX query engine' {
        foreach ($Name in 'EventId', 'EventRecordId', 'ProviderName', 'Keywords', 'Level', 'StartTime', 'EndTime', 'TimePeriod', 'UserId') {
            $PathParameters | Should -Contain $Name
        }
    }

    It 'uses the canonical native concurrency controls for EVTX reads' {
        $PathParameters | Should -Not -Contain 'ParallelOption'
        $PathParameters | Should -Contain 'DisableParallel'
        $PathParameters | Should -Contain 'MaxConcurrency'
    }

    It 'keeps offline files local instead of accepting an ignored remote target' {
        $PathParameters | Should -Not -Contain 'MachineName'
        $PathParameters | Should -Not -Contain 'Credential'
        $PathParameters | Should -Not -Contain 'Authentication'
    }

    It 'rejects remote options for file-only FilterHashtable queries' {
        {
            Get-EVXEvent `
                -FilterHashtable @{
                    Path = 'C:\missing.evtx'
                } `
                -MachineName 'server.example.test' `
                -ErrorAction Stop
        } | Should -Throw '*always read locally*'
    }

    It 'rejects a remote target for file paths embedded in FilterXml' {
        [xml] $Query = @'
<QueryList>
  <Query Id="0" Path="System">
    <Select Path="System">*</Select>
  </Query>
  <Query Id="1" Path="file:///C:/events.evtx">
    <Select Path="file:///C:/events.evtx">*</Select>
  </Query>
</QueryList>
'@

        {
            Get-EVXEvent `
                -FilterXml $Query `
                -MachineName 'remote.contoso.test' `
                -ContinueOnError `
                -ErrorAction Stop
        } | Should -Throw '*cannot be combined with MachineName*'
    }


    It 'supports explicit provider-message culture for offline, live, and named queries' {
        $PathParameters | Should -Contain 'MessageCulture'
        $GenericParameters = (Get-Command Get-EVXEvent).ParameterSets |
            Where-Object Name -EQ 'Channel' |
            Select-Object -ExpandProperty Parameters |
            Select-Object -ExpandProperty Name
        $GenericParameters | Should -Contain 'MessageCulture'
        $NamedParameters = (Get-Command Get-EVXEvent).ParameterSets |
            Where-Object Name -EQ 'Type' |
            Select-Object -ExpandProperty Parameters |
            Select-Object -ExpandProperty Name
        $NamedParameters | Should -Contain 'MessageCulture'

        $Event = Get-EVXEvent `
            -LogName System `
            -MaxEvents 1 `
            -ReadMode Message `
            -MessageCulture en-US
        $Event.MessageCulture | Should -Be 'en-US'
        $Event.MessageRenderStatus.ToString() | Should -Not -Be 'NotRequested'

        $NamedEvent = Get-EVXEvent `
            -Type OSStartup `
            -MaxEvents 1 `
            -MessageCulture en-US `
            -ErrorAction Stop |
            Select-Object -First 1
        if ($null -eq $NamedEvent) {
            Set-ItResult -Skipped -Because 'No OSStartup event was available.'
        } else {
            $NamedEvent.SourceEvent.MessageCulture | Should -Be 'en-US'
            $NamedEvent.SourceEvent.MessageRenderStatus.ToString() | Should -Not -Be 'NotRequested'
        }
    }
}
