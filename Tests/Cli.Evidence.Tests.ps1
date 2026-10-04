Describe 'evx privacy and evidence export contracts' {
    BeforeAll {
        $script:EvidenceCliPath = $null
        foreach ($Candidate in @($env:EVX_CLI_PATH,
                (Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Release\net10.0\evx.exe'),
                (Join-Path $PSScriptRoot '..\Sources\EventViewerX.Cli\bin\Debug\net10.0\evx.exe'))) {
            if (-not [string]::IsNullOrWhiteSpace($Candidate) -and (Test-Path -LiteralPath $Candidate)) {
                $script:EvidenceCliPath = $Candidate
                break
            }
        }
        if (-not $script:EvidenceCliPath) { throw 'Build EventViewerX.Cli for net10.0 or set EVX_CLI_PATH.' }
        $script:EvidenceFixture = Join-Path $PSScriptRoot 'Logs\NamedFilterExamples.evtx'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
    }

    It 'exports an omitted report with equivalent strict completion and a verifiable inventory' {
        $Bundle = Join-Path $TestDrive 'omitted.zip'
        $Csv = Join-Path $TestDrive 'omitted.csv'
        $Summary = Join-Path $TestDrive 'summary.json'
        & $script:EvidenceCliPath report --path $script:EvidenceFixture --oldest --max 2 --privacy omit `
            --bundle $Bundle --csv $Csv --summary-file $Summary --require-complete | Out-Null
        $LASTEXITCODE | Should -Be 2
        $Verified = & $script:EvidenceCliPath bundle verify --path $Bundle | ConvertFrom-Json
        $LASTEXITCODE | Should -Be 0
        $Verified.SchemaVersion | Should -Be 1
        $Verified.Summary.IsComplete | Should -BeFalse
        $Verified.Summary.EventCount | Should -Be 2
        ($Verified.Entries.Name -contains 'query.json') | Should -BeFalse
        ($Verified.Entries.Name -contains 'privacy.json') | Should -BeTrue
        ($Verified.Entries.Name -contains 'report.csv.metadata.json') | Should -BeTrue
        (Get-Content -LiteralPath $Summary -Raw | ConvertFrom-Json).IsComplete | Should -BeFalse
        (Get-Content -LiteralPath $Csv -Raw) | Should -Not -Match 'SmsRouter'
    }

    It 'exports stable keyed payload tokens without raw text or the key' {
        $Key = Join-Path $TestDrive 'key.bin'
        [IO.File]::WriteAllBytes($Key, [byte[]] (0..31))
        $Bundle = Join-Path $TestDrive 'pseudonymized.zip'
        & $script:EvidenceCliPath report --path $script:EvidenceFixture --oldest --max 1 --privacy pseudonymize `
            --privacy-key-file $Key --pseudonymize-fields param1 --bundle $Bundle | Out-Null
        $LASTEXITCODE | Should -Be 0
        $Archive = [IO.Compression.ZipFile]::OpenRead($Bundle)
        try {
            $Reader = New-Object IO.StreamReader ($Archive.GetEntry('rows.jsonl').Open())
            try { $Text = $Reader.ReadToEnd() } finally { $Reader.Dispose() }
        } finally { $Archive.Dispose() }
        $Row = $Text | ConvertFrom-Json
        $Row.param1 | Should -Match '^hmac-sha256:[0-9a-f]{64}$'
        $Text | Should -Not -Match 'SmsRouter'
        [IO.File]::ReadAllBytes($Key).Length | Should -Be 32
    }

    It 'rejects output over an input or a colliding sidecar before modifying source bytes' -TestCases @(
        @{ Collision = 'input' }
        @{ Collision = 'extended-input' }
        @{ Collision = 'sidecar' }
    ) {
        param($Collision)
        $Copy = Join-Path $TestDrive ('input-' + $Collision + '.evtx')
        Copy-Item -LiteralPath $script:EvidenceFixture -Destination $Copy
        $Before = (Get-FileHash -LiteralPath $Copy -Algorithm SHA256).Hash
        $Options = @('report', '--path', $Copy, '--max', '1')
        if ($Collision -eq 'sidecar') {
            $Csv = Join-Path $TestDrive 'collision.csv'
            $Options += @('--csv', $Csv, '--summary-file', ($Csv + '.metadata.json'))
        } else {
            $Output = if ($Collision -eq 'extended-input') { '\\?\' + $Copy } else { $Copy }
            $Options += @('--html', $Output)
        }
        $PreviousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $ErrorText = & $script:EvidenceCliPath @Options 2>&1
            $ExitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $PreviousPreference }
        $ExitCode | Should -Be 1
        [string] $ErrorText | Should -Match 'must be separate'
        (Get-FileHash -LiteralPath $Copy -Algorithm SHA256).Hash | Should -Be $Before
    }
}
