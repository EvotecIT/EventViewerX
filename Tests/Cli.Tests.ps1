Describe 'evx portable host' {
    BeforeAll {
        $script:CliPath = Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Debug\net10.0-windows\evx.exe'
        $script:FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $script:SmtpProfilePath = Join-Path $PSScriptRoot 'Fixtures\SmtpProfile.DryRun.json'
    }

    It 'ships the complete built-in type catalog' {
        $Definitions = @(& $script:CliPath types | ForEach-Object { $_ | ConvertFrom-Json })

        $LASTEXITCODE | Should -Be 0
        $Definitions.Count | Should -Be ([Enum]::GetValues([EventViewerX.EventType]).Count)
        @($Definitions | Where-Object { -not $_.IsComposite }).Count | Should -Be 89
        @($Definitions | Where-Object IsComposite).Count | Should -Be 14
        $Definitions.Name | Should -Contain 'GroupPolicyDirectoryAudit'
        $Definitions.Name | Should -Contain 'AuthenticationHealth'
        $Definitions.Name | Should -Contain 'DefenderSecurity'
    }

    It 'queries offline files without PowerShell module startup' {
        $Rows = @(& $script:CliPath query --path $script:FixturePath --max 3 |
                ForEach-Object { $_ | ConvertFrom-Json })

        $LASTEXITCODE | Should -Be 0
        $Rows.Count | Should -Be 3
        $Rows[0].Type | Should -Be 'Generic'
        $Rows[0].SourceLog | Should -Be 'System'
        $Rows[0].Message | Should -Not -BeNullOrEmpty
    }

    It 'emits canonical aliases and normalization evidence in query JSON' {
        $Row = & $script:CliPath query --path $script:FixturePath --max 1 |
            ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Row.ProviderName | Should -Be $Row.Provider
        $Row.TypeName | Should -Be $Row.Type
        $Row._EventViewerX.Normalization | Should -Not -BeNullOrEmpty
    }

    It 'renders HTML and Excel from one query and composes a Mailozaurr delivery' {
        $HtmlPath = Join-Path $TestDrive 'events.html'
        $ExcelPath = Join-Path $TestDrive 'events.xlsx'
        $EmailPath = Join-Path $TestDrive 'events-email.html'
        $Output = @(& $script:CliPath report `
                --path $script:FixturePath `
                --max 3 `
                --html $HtmlPath `
                --drawer-placement Top `
                --excel $ExcelPath `
                --email-html $EmailPath `
                --mail-profile $script:SmtpProfilePath)

        $LASTEXITCODE | Should -Be 0
        Test-Path -LiteralPath $HtmlPath | Should -BeTrue
        (Get-Content -LiteralPath $HtmlPath -Raw) | Should -Match 'data-hfx-monitoring-record-drawer-placement="top"'
        Test-Path -LiteralPath $ExcelPath | Should -BeTrue
        Test-Path -LiteralPath $EmailPath | Should -BeTrue
        $Delivery = $Output[-1] | ConvertFrom-Json
        $Delivery.DryRun | Should -BeTrue
        $Delivery.Delivered | Should -BeFalse
    }

    It 'rejects an unknown HTML drawer placement' {
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath report --path $script:FixturePath --html (Join-Path $TestDrive 'invalid.html') --drawer-placement Bottom 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match 'Auto, Top, or Right'
    }

    It 'rejects ambiguous query sources' {
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query --path $script:FixturePath --log System 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match 'standalone --path'
    }

    It 'rejects typed predicates on generic live or offline sources before ingestion' {
        $PredicatePath = Join-Path $TestDrive 'generic-predicate.json'
        $StorePath = Join-Path $TestDrive 'generic-rejected.db'
        @{
            Kind = 'Comparison'
            Field = 'EventId'
            Operator = 'Equal'
            Values = @('7040')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --path $script:FixturePath `
                --where $PredicatePath `
                --write-store $StorePath 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--where requires --type or --definition'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'rejects generic event ID selectors on typed sources before ingestion' {
        $StorePath = Join-Path $TestDrive 'typed-event-id-rejected.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --type ADUserLogonFailed `
                --event-id 4625 `
                --write-store $StorePath 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--event-id is available only for generic'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'rejects explanation combined with event-store ingestion before source access' {
        $PredicatePath = Join-Path $TestDrive 'explain-write-store-predicate.json'
        $StorePath = Join-Path $TestDrive 'explain-write-store-rejected.db'
        @{
            Field = 'EventId'
            Operator = 'Equal'
            Values = @('4625')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --type ADUserLogonFailed `
                --where $PredicatePath `
                --explain `
                --write-store $StorePath 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--explain cannot be combined with --write-store'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'rejects live-only controls for stored queries before opening history' {
        $StorePath = Join-Path $TestDrive 'stored-live-options-rejected.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $ResolveDnsOutput = & $script:CliPath query `
                --store $StorePath `
                --resolve-dns 2>&1
            $ResolveDnsExitCode = $LASTEXITCODE
            $ConcurrencyOutput = & $script:CliPath query `
                --store $StorePath `
                --concurrency 4 2>&1
            $ConcurrencyExitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $ResolveDnsExitCode | Should -Be 1
        $ConcurrencyExitCode | Should -Be 1
        [string] $ResolveDnsOutput | Should -Match 'live event-source options'
        [string] $ConcurrencyOutput | Should -Match 'live event-source options'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'uses managed occurrence grouping before aggregating stored history' {
        $StorePath = Join-Path $TestDrive 'occurrence-measure.db'
        $null = & $script:CliPath query `
            --path $script:FixturePath `
            --oldest `
            --max 4 `
            --write-store $StorePath

        $Result = & $script:CliPath measure `
            --store $StorePath `
            --duplicates Transport `
            --group-by Type | ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Result.ExecutionMode | Should -Be 'Managed'
        $Result.InputRows | Should -Be 4
        $Result.Rows[0].Measures.Count | Should -Be 4
    }

    It 'rejects occurrence grouping over already-derived stored summaries' {
        $StorePath = Join-Path $TestDrive 'summary-occurrence-rejected.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath report `
                --store $StorePath `
                --summary Day `
                --duplicates Semantic `
                --html (Join-Path $TestDrive 'not-created.html') 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match 'already derived data'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'rejects mixed stored definition selector families before opening history' {
        $StorePath = Join-Path $TestDrive 'mixed-stored-selectors.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --store $StorePath `
                --type ADUserLogonFailed `
                --definition-name CustomLogon 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--type and --definition-name are mutually exclusive'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'normalizes built-in predicates before producing an explanation' {
        $PredicatePath = Join-Path $TestDrive 'invalid-built-in-predicate.json'
        @{
            Kind = 'Comparison'
            Field = 'EventId'
            Operator = 'Equal'
            Values = @('not-an-event-id')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --type ADUserLogonFailed `
                --where $PredicatePath `
                --explain 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match "not valid for field 'EventId'"
    }

    It 'rejects unknown options instead of silently running an unbounded query' {
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query --path $script:FixturePath --max-events 1 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match "Unknown option\(s\): --max-events"
    }

    It 'rejects positional arguments instead of silently treating them as subcommands' {
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query ignored --path $script:FixturePath --max 1 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match "Unexpected argument 'ignored'"
    }

    It 'applies a custom definition to an offline file' {
        $DefinitionPath = Join-Path $TestDrive 'service-change.json'
        $PredicatePath = Join-Path $TestDrive 'service-change-predicate.json'
        @{
            Name = 'ServiceStartTypeChange'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(
                @{
                    Name = 'ProjectedId'
                    ValueKind = 'Int32'
                    Source = 'Metadata'
                    SourceName = 'EventId'
                }
                @{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param1'
                }
            )
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        @{
            Kind = 'Comparison'
            Field = 'ProjectedId'
            Operator = 'Equal'
            Values = @('7040')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8

        $Rows = @(& $script:CliPath query --definition $DefinitionPath --path $script:FixturePath --max 2 |
                ForEach-Object { $_ | ConvertFrom-Json })
        $Plan = & $script:CliPath query `
            --definition $DefinitionPath `
            --path $script:FixturePath `
            --where $PredicatePath `
            --explain |
            ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Rows.Count | Should -Be 2
        @($Rows.Type | Sort-Object -Unique) | Should -Be @('ServiceStartTypeChange')
        @($Rows.ProjectedId | Sort-Object -Unique) | Should -Be @(7040)
        $Plan.NativeFilter.EventIds | Should -Be 7040
        $Plan.ManagedPredicate | Should -Not -BeNullOrEmpty
    }

    It 'stores an offline report and renders a calendar summary without rereading EVTX' {
        $StorePath = Join-Path $TestDrive 'cli-events.db'
        $HtmlPath = Join-Path $TestDrive 'cli-summary.html'
        $PredicatePath = Join-Path $TestDrive 'cli-event-id-predicate.json'
        @{
            Field = 'EventId'
            Operator = 'Equal'
            Values = @('7040')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8

        $Rows = @(& $script:CliPath query `
                --path $script:FixturePath `
                --max 4 `
                --write-store $StorePath |
                ForEach-Object { $_ | ConvertFrom-Json })
        $SummaryOutput = @(& $script:CliPath report `
                --store $StorePath `
                --summary Day `
                --html $HtmlPath)
        $Plan = & $script:CliPath query `
            --store $StorePath `
            --where $PredicatePath `
            --explain |
            ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Rows.Count | Should -Be 4
        Test-Path -LiteralPath $StorePath | Should -BeTrue
        Test-Path -LiteralPath $HtmlPath | Should -BeTrue
        (Get-Content -LiteralPath $HtmlPath -Raw) | Should -Match 'Day event summary'
        $SummaryOutput[-1] | Should -Be ([IO.Path]::GetFullPath($HtmlPath))
        @($Plan.Steps | Where-Object Expression -Like 'EventId *').Stage | Should -Contain 'Managed'
    }

    It 'normalizes stored custom predicates with their definition metadata' {
        $DefinitionPath = Join-Path $TestDrive 'stored-alias-definition.json'
        $PredicatePath = Join-Path $TestDrive 'stored-alias-predicate.json'
        $StorePath = Join-Path $TestDrive 'stored-alias-events.db'
        @{
            Name = 'StoredAliasDefinition'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(@{
                    Name = 'ServiceLabel'
                    Aliases = @('ProviderName')
                    Source = 'Data'
                    SourceName = 'param4'
                })
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8
        @{
            Kind = 'Comparison'
            Field = 'ProviderName'
            Operator = 'Equal'
            Values = @('BITS')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8

        $null = @(& $script:CliPath query `
                --definition $DefinitionPath `
                --path $script:FixturePath `
                --max 10 `
                --write-store $StorePath)
        $Rows = @(& $script:CliPath query `
                --store $StorePath `
                --definition $DefinitionPath `
                --where $PredicatePath `
                --oldest |
                ForEach-Object { $_ | ConvertFrom-Json })
        $Plan = & $script:CliPath query `
            --store $StorePath `
            --definition $DefinitionPath `
            --where $PredicatePath `
            --explain |
            ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Rows | Should -Not -BeNullOrEmpty
        @($Rows.ServiceLabel | Sort-Object -Unique) | Should -Be @('BITS')
        $Plan.Steps.Expression | Should -Contain 'ServiceLabel Equal BITS'
        $Plan.Steps.Stage | Should -Contain 'Managed'
    }

    It 'renders empty stored custom CSV with the supplied definition schema' {
        $DefinitionPath = Join-Path $TestDrive 'empty-stored-definition.json'
        $StorePath = Join-Path $TestDrive 'empty-stored-events.db'
        $CsvPath = Join-Path $TestDrive 'empty-stored-events.csv'
        @{
            Name = 'EmptyStoredAudit'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(
                @{
                    Name = 'ServiceName'
                    Source = 'Data'
                    SourceName = 'param4'
                }
                @{
                    Name = 'ProjectedId'
                    ValueKind = 'Int32'
                    Source = 'Metadata'
                    SourceName = 'EventId'
                }
            )
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $Output = @(& $script:CliPath report `
                --store $StorePath `
                --definition $DefinitionPath `
                --csv $CsvPath)

        $LASTEXITCODE | Should -Be 0
        $Output[-1] | Should -Be ([IO.Path]::GetFullPath($CsvPath))
        Test-Path -LiteralPath $CsvPath | Should -BeTrue
        (Get-Content -LiteralPath $CsvPath -TotalCount 1) | Should -Match 'Service Name'
        (Get-Content -LiteralPath $CsvPath -TotalCount 1) | Should -Match 'Projected ID'
    }

    It 'preserves declared custom fields that shadow native metadata in live and stored JSON' {
        $DefinitionPath = Join-Path $TestDrive 'shadowing-definition.json'
        $StorePath = Join-Path $TestDrive 'shadowing-events.db'
        @{
            Name = 'CliShadowingDefinition'
            Sources = @(@{
                    LogName = 'System'
                    EventIds = @(7040)
                    ProviderNames = @('Service Control Manager')
                })
            Fields = @(
                @{
                    Name = 'EventId'
                    Source = 'Constant'
                    SourceName = 'domain-event-id'
                }
                @{
                    Name = 'Provider'
                    Source = 'Constant'
                    SourceName = 'domain-provider'
                }
                @{
                    Name = 'Normalization'
                    Source = 'Constant'
                    SourceName = 'domain-normalization'
                }
            )
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $DefinitionPath -Encoding UTF8

        $LiveRows = @(& $script:CliPath query `
                --definition $DefinitionPath `
                --path $script:FixturePath `
                --max 1 `
                --write-store $StorePath |
                ForEach-Object { $_ | ConvertFrom-Json })
        $StoredRows = @(& $script:CliPath query `
                --store $StorePath `
                --definition $DefinitionPath `
                --max 1 |
                ForEach-Object { $_ | ConvertFrom-Json })

        $LASTEXITCODE | Should -Be 0
        $LiveRows | Should -HaveCount 1
        $StoredRows | Should -HaveCount 1
        $LiveRows[0].EventId | Should -Be 'domain-event-id'
        $LiveRows[0].Provider | Should -Be 'domain-provider'
        $LiveRows[0].Normalization | Should -Be 'domain-normalization'
        $LiveRows[0]._EventViewerX.Normalization | Should -Not -BeNullOrEmpty
        $StoredRows[0].EventId | Should -Be 'domain-event-id'
        $StoredRows[0].Provider | Should -Be 'domain-provider'
        $StoredRows[0].Normalization | Should -Be 'domain-normalization'
        $StoredRows[0]._EventViewerX.Normalization | Should -Not -BeNullOrEmpty
    }

    It 'removes an already absent collector subscription idempotently' {
        $Name = 'EVX-Cli-Absent-' + [guid]::NewGuid().ToString('N')
        $Result = & $script:CliPath collector remove --name $Name |
            ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Result.SubscriptionName | Should -Be $Name
        $Result.Success | Should -BeTrue
        $Result.Changed | Should -BeFalse
        $Result.Before | Should -BeNullOrEmpty
        $Result.After | Should -BeNullOrEmpty
    }
}
