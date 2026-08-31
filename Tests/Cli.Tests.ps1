Describe 'evx portable host' {
    BeforeAll {
        $cliCandidates = @(
            $env:EVX_CLI_PATH
            (Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Release\net10.0\evx.exe')
            (Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Debug\net10.0\evx.exe')
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $script:CliPath = $cliCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $script:CliPath) {
            throw 'Build EventViewerX.Cli for net10.0 or set EVX_CLI_PATH before running CLI tests.'
        }
        $script:FixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        $script:TruncatedFixturePath = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples-Truncated.evtx'
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

    It 'dry-runs detection without opening an event source' {
        $Plan = & $script:CliPath detect --dry-run |
            ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Plan.RuleCount | Should -BeGreaterThan 0
        $Plan.PlanHash | Should -Not -BeNullOrEmpty
    }

    It 'rejects an invalid watch flush interval before opening subscriptions' {
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath watch --type OSStartup --interval 00:00:00 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match 'Flush interval must be greater than zero'
    }

    It 'emits canonical versioned finding and trace JSON contracts' {
        $SigmaPath = Join-Path $TestDrive 'canonical-json.yml'
        $FindingPath = Join-Path $TestDrive 'findings.jsonl'
        $TracePath = Join-Path $TestDrive 'traces.jsonl'
        @'
title: Service configuration changed
id: bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb
logsource:
  product: windows
  service: system
detection:
  selection:
    EventID: 7040
  condition: selection
level: medium
'@ | Set-Content -LiteralPath $SigmaPath -Encoding UTF8

        $null = & $script:CliPath detect `
            --sigma $SigmaPath `
            --path $script:FixturePath `
            --max 0 `
            --jsonl $FindingPath `
            --trace-jsonl $TracePath

        $LASTEXITCODE | Should -Be 0
        $Finding = Get-Content -LiteralPath $FindingPath -TotalCount 1 | ConvertFrom-Json
        $Trace = Get-Content -LiteralPath $TracePath -TotalCount 1 | ConvertFrom-Json
        $Finding.schemaVersion | Should -Be 1
        $Finding.evidenceIdentities.Count | Should -BeGreaterThan 0
        $Finding.PSObject.Properties.Name | Should -Not -Contain 'evidence'
        $Trace.schemaVersion | Should -Be 1
        $Trace.observationIdentity | Should -Not -BeNullOrEmpty
        $Trace.conditions | Should -Not -BeNull
    }

    It 'marks detection incomplete when an explicit event ID excludes the rule selector' {
        $SigmaPath = Join-Path $TestDrive 'selector-coverage.yml'
        @'
title: Service configuration changed
id: 66666666-6666-4666-8666-666666666666
logsource:
  product: windows
  service: system
detection:
  selection:
    EventID: 7040
  condition: selection
level: medium
'@ | Set-Content -LiteralPath $SigmaPath -Encoding UTF8

        $null = & $script:CliPath detect `
            --sigma $SigmaPath `
            --path $script:FixturePath `
            --event-id 1 `
            --max 10

        $LASTEXITCODE | Should -Be 2
    }

    It 'renders a detection report when an output consumes the report snapshot' {
        $SigmaPath = Join-Path $TestDrive 'report-detection.yml'
        $HtmlPath = Join-Path $TestDrive 'detection.html'
        @'
title: Service configuration changed
id: 77777777-7777-4777-8777-777777777777
logsource:
  product: windows
  service: system
detection:
  selection:
    EventID: 7040
  condition: selection
level: medium
'@ | Set-Content -LiteralPath $SigmaPath -Encoding UTF8

        $Output = @(& $script:CliPath detect `
                --sigma $SigmaPath `
                --path $script:FixturePath `
                --max 0 `
                --report-html $HtmlPath)

        $LASTEXITCODE | Should -Be 0
        Test-Path -LiteralPath $HtmlPath | Should -BeTrue
        $Output[-1] | Should -Be ([IO.Path]::GetFullPath($HtmlPath))
    }

    It 'marks generic detection incomplete only when the source limit truncates matching events' {
        $SigmaPath = Join-Path $TestDrive 'source-limit-detection.yml'
        @'
title: Service configuration changed
id: 88888888-8888-4888-8888-888888888888
logsource:
  product: windows
  service: system
detection:
  selection:
    EventID: 7040
  condition: selection
level: medium
'@ | Set-Content -LiteralPath $SigmaPath -Encoding UTF8

        $null = & $script:CliPath detect `
            --sigma $SigmaPath `
            --path $script:FixturePath `
            --max 1

        $LASTEXITCODE | Should -Be 2
    }

    It 'propagates portable parser warnings into detection coverage' {
        $SigmaPath = Join-Path $TestDrive 'portable-coverage-detection.yml'
        @'
title: Service configuration changed
id: 99999999-9999-4999-8999-999999999999
logsource:
  product: windows
  service: system
detection:
  selection:
    EventID: 7040
  condition: selection
level: medium
'@ | Set-Content -LiteralPath $SigmaPath -Encoding UTF8

        $null = & $script:CliPath detect `
            --sigma $SigmaPath `
            --path $script:TruncatedFixturePath `
            --portable-evtx `
            --max 0

        $LASTEXITCODE | Should -Be 2
    }

    It 'keeps generic log coverage incomplete when the plan requires other channels' {
        $SigmaPath = Join-Path $TestDrive 'generic-log-coverage.yml'
        @'
title: Service configuration changed
id: aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa
logsource:
  product: windows
  service: system
detection:
  selection:
    EventID: 7040
  condition: selection
level: medium
'@ | Set-Content -LiteralPath $SigmaPath -Encoding UTF8

        $null = & $script:CliPath detect `
            --sigma $SigmaPath `
            --log Application `
            --since 00:00:01 `
            --max 1

        $LASTEXITCODE | Should -Be 2
    }

    It 'rejects generic event ID and provider selectors on typed detection sources' {
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $EventIdOutput = & $script:CliPath detect `
                --type ADUserLogonFailed `
                --path $script:FixturePath `
                --event-id 4625 2>&1
            $EventIdExitCode = $LASTEXITCODE
            $ProviderOutput = & $script:CliPath detect `
                --type ADUserLogonFailed `
                --path $script:FixturePath `
                --provider Microsoft-Windows-Security-Auditing 2>&1
            $ProviderExitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $EventIdExitCode | Should -Be 1
        $ProviderExitCode | Should -Be 1
        [string] $EventIdOutput | Should -Match '--event-id and --provider are available only for generic'
        [string] $ProviderOutput | Should -Match '--event-id and --provider are available only for generic'
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

    It 'rejects contextual explanation before creating persistent state' {
        $ContextPath = Join-Path $TestDrive 'context-explain-rejected.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --type GroupPolicyDirectoryAudit `
                --path $script:FixturePath `
                --context-store $ContextPath `
                --explain 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--explain cannot be combined with --context-store'
        Test-Path -LiteralPath $ContextPath | Should -BeFalse
    }

    It 'rejects mixed direct and collector targets before opening persistent context' {
        $ContextPath = Join-Path $TestDrive 'context-mixed-targets-rejected.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --type GroupPolicyDirectoryAudit `
                --path $script:FixturePath `
                --context-store $ContextPath `
                --machine source01.ad.evotec.xyz `
                --collector collector01.ad.evotec.xyz 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--machine and --collector are mutually exclusive'
        Test-Path -LiteralPath $ContextPath | Should -BeFalse
    }

    It 'validates live measure definitions before opening persistent context' {
        $ContextPath = Join-Path $TestDrive 'context-invalid-measure-rejected.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath measure `
                --type GroupPolicyDirectoryAudit `
                --path $script:FixturePath `
                --context-store $ContextPath `
                --timezone 'Invalid/EventViewerX-Zone' 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match 'time zone|timezone|not found'
        Test-Path -LiteralPath $ContextPath | Should -BeFalse
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
        $Result.Rows[0].Group.Type | Should -Be 'Generic'
    }

    It 'validates occurrence aggregation definitions before explain output or store creation' {
        $StorePath = Join-Path $TestDrive 'invalid-occurrence-explain.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath measure `
                --store $StorePath `
                --duplicates Semantic `
                --explain `
                --measure 'Rate::PerHour:01:00:00' 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match 'unbucketed Rate'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'validates query occurrence options before opening persistent output' {
        $StorePath = Join-Path $TestDrive 'invalid-query-occurrence.db'
        $PreviousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Output = & $script:CliPath query `
                --path $script:FixturePath `
                --duplicates NotAMode `
                --write-store $StorePath 2>&1
        } finally {
            $ErrorActionPreference = $PreviousErrorActionPreference
        }

        $LASTEXITCODE | Should -Be 1
        [string] $Output | Should -Match '--duplicates has an unsupported value'
        Test-Path -LiteralPath $StorePath | Should -BeFalse
    }

    It 'emits an explicit metadata record when occurrence bounds fail closed' {
        $Result = & $script:CliPath query `
            --path $script:FixturePath `
            --oldest `
            --max 4 `
            --duplicates Transport `
            --maximum-occurrence-observations 1 | ConvertFrom-Json

        $LASTEXITCODE | Should -Be 0
        $Result.ResultKind | Should -Be 'ResultMetadata'
        $Result.IsComplete | Should -BeFalse
        $Result.Diagnostic | Should -Match 'MaximumObservations'
    }

    It 'writes aggregation completeness evidence into a plain CSV' {
        $CsvPath = Join-Path $TestDrive 'bounded-aggregation.csv'

        $null = & $script:CliPath measure `
            --path $script:FixturePath `
            --max 4 `
            --group-by RecordId `
            --maximum-groups 1 `
            --csv $CsvPath
        $Rows = @(Import-Csv -LiteralPath $CsvPath)

        $LASTEXITCODE | Should -Be 0
        $Rows | Should -HaveCount 1
        $Rows[0].'Result kind' | Should -Be 'ResultMetadata'
        $Rows[0].'Aggregation complete' | Should -Be 'False'
        $Rows[0].Diagnostic | Should -Match 'MaximumGroups'
        $Rows[0].'Input rows' | Should -Be '2'
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

    It 'queries stored Group Policy aliases through the enriched schema' {
        $StorePath = Join-Path $TestDrive 'cli-gpo-audit.db'
        $PredicatePath = Join-Path $TestDrive 'cli-gpo-audit-predicate.json'
        $Row = [EventViewerX.Reporting.EventReportRow]::new()
        $Row.TimeCreated = [datetime]::SpecifyKind([datetime]'2026-08-23T10:00:00', [DateTimeKind]::Utc)
        $Row.Type = 'GroupPolicyAudit'
        $Row.EventId = 5136
        $Row.RecordId = 42
        $Row.Provider = 'Microsoft-Windows-Security-Auditing'
        $Row.SourceLog = 'Security'
        $Row.ContainerLog = 'ForwardedEvents'
        $Row.SourceComputer = 'dc1.ad.evotec.xyz'
        $Row.CollectorComputer = 'wec1.ad.evotec.xyz'
        $Values = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
        $Values['Actor'] = 'AD\alice'
        $Values['GroupPolicyNameAtEventTime'] = 'Workstation Baseline'
        $Row.Values = $Values
        $Report = [EventViewerX.Reporting.EventReportEngine]::CreateStored(
            [EventViewerX.Reporting.EventReportRow[]]@($Row),
            [EventViewerX.Reporting.EventReportSectionSchema[]]@(
                [EventViewerX.Reporting.EventReportSectionSchema]::FromGroupPolicyAudit()))
        $null = [EventViewerX.Storage.EventStore]::new($StorePath).WriteAsync($Report).GetAwaiter().GetResult()
        @{
            Field = 'Actor'
            Operator = 'Equal'
            Values = @('AD\alice')
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $PredicatePath -Encoding UTF8

        $Rows = @(& $script:CliPath query `
                --store $StorePath `
                --type GroupPolicyDirectoryAudit `
                --where $PredicatePath |
                ForEach-Object { $_ | ConvertFrom-Json })

        $LASTEXITCODE | Should -Be 0
        $Rows.Count | Should -Be 1
        $Rows[0].GroupPolicyNameAtEventTime | Should -Be 'Workstation Baseline'
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
