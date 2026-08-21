Describe 'New-EVXCollectorSubscription' {
    It 'creates one typed definition from a reusable EventFilter' {
        $Filter = New-EVXFilter `
            -EventId 4625 `
            -ProviderName Microsoft-Windows-Security-Auditing `
            -TimePeriod Last24Hours

        $Definition = New-EVXCollectorSubscription `
            -Name FailedLogons `
            -SourceComputer DC01, DC02 `
            -LogName Security `
            -Filter $Filter `
            -Description 'Failed logons from domain controllers' `
            -Enabled $false `
            -MaxItems 5 `
            -MaxLatencyMilliseconds 2000 `
            -HeartbeatIntervalMilliseconds 30000

        $Definition.GetType().FullName |
            Should -Be 'EventViewerX.CollectorSubscriptionDefinition'
        $Definition.SubscriptionId | Should -Be 'FailedLogons'
        @($Definition.Sources | ForEach-Object Address) |
            Should -Be @('DC01', 'DC02')
        $Definition.Enabled | Should -BeFalse
        $Definition.MaxItems | Should -Be 5
        $Definition.QueryXml | Should -BeLike '*4625*'
        $Definition.QueryXml | Should -BeLike '*Microsoft-Windows-Security-Auditing*'
    }

    It 'writes Windows-compatible XML without changing the collector' {
        $OutputPath = Join-Path $TestDrive 'subscription.xml'

        $File = New-EVXCollectorSubscription `
            -Name SystemErrors `
            -SourceComputer SRV01 `
            -LogName System `
            -Level 2 `
            -Enabled $false `
            -OutputPath $OutputPath

        $File.FullName | Should -Be $OutputPath
        [xml] $Xml = Get-Content -LiteralPath $OutputPath -Raw
        $Xml.Subscription.SubscriptionId | Should -Be 'SystemErrors'
        $Xml.Subscription.EventSources.EventSource.Address | Should -Be 'SRV01'
        $Xml.Subscription.Query.'#cdata-section' | Should -BeLike '*Level=2*'
    }

    It 'applies typed definitions only through the explicit Set cmdlet' {
        $Definition = New-EVXCollectorSubscription `
            -Name ('PSEventViewer-WhatIf-' + [guid]::NewGuid().ToString('N')) `
            -SourceComputer $env:COMPUTERNAME `
            -LogName System `
            -Enabled $false

        $Result = $Definition | Set-EVXCollectorSubscription -WhatIf

        $Result | Should -BeNullOrEmpty
        Get-EVXCollectorSubscription -Name $Definition.SubscriptionId |
            Should -BeNullOrEmpty
    }

    It 'compiles a built-in Type without a caller-supplied LogName' {
        $Definition = New-EVXCollectorSubscription `
            -Name FailedLogons `
            -SourceComputer DC01 `
            -Type ADUserLogonFailed `
            -Enabled $false

        [xml] $Query = $Definition.QueryXml
        $Query.QueryList.Query.Path | Should -Contain 'Security'
        ($Definition.QueryXml) | Should -Match '4625'
        ((Get-Command New-EVXCollectorSubscription).ParameterSets |
                Where-Object Name -EQ 'Type').Parameters.Name |
            Should -Not -Contain 'LogName'
    }

    It 'creates a source-initiated push definition without enumerating sources' {
        $DomainControllersSid =
            'S-1-5-21-111111111-222222222-333333333-516'
        $Definition = New-EVXCollectorSubscription `
            -Name DomainControllerSecurity `
            -SubscriptionType SourceInitiated `
            -CollectorHostName wec01.ad.evotec.xyz `
            -AllowedSourceSid $DomainControllersSid `
            -LogName Security `
            -EventId 4624 `
            -Enabled $false

        [xml] $Xml = $Definition.ToXml()
        $Xml.Subscription.SubscriptionType | Should -Be 'SourceInitiated'
        $Xml.Subscription.Delivery.Mode | Should -Be 'Push'
        $Xml.Subscription.AllowedSourceDomainComputers | Should -Match '-516'
        $Xml.Subscription.EventSources | Should -BeNullOrEmpty
        $Definition.SourceSubscriptionManagerValue | Should -Be (
            'Server=http://wec01.ad.evotec.xyz:5985/' +
            'wsman/SubscriptionManager/WEC,Refresh=60')
    }
}
