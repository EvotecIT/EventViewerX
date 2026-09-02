Describe 'Export-EVXEvent direct streaming contract' {
    BeforeAll {
        $SourcePath = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'NamedFilterExamples.evtx'
        )
        $OutputDirectory = [System.IO.Path]::Combine(
            [System.IO.Path]::GetTempPath(),
            "PSEventViewer-Export-$([guid]::NewGuid().ToString('N'))"
        )
        [System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    }

    AfterAll {
        if ([System.IO.Directory]::Exists($OutputDirectory)) {
            [System.IO.Directory]::Delete($OutputDirectory, $true)
        }
    }

    It 'exports complete English JSON Lines without a PowerShell object pipeline' {
        $OutputPath = [System.IO.Path]::Combine($OutputDirectory, 'events.jsonl')

        $Result = Export-EVXEvent `
            -Path $SourcePath `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Full `
            -MessageCulture en-US `
            -Oldest `
            -MaxEvents 5

        $Lines = [System.IO.File]::ReadAllLines($OutputPath)
        $First = $Lines[0] | ConvertFrom-Json
        $Result.EventCount | Should -Be 5
        $Lines.Count | Should -Be 5
        $First.messageCulture | Should -Be 'en-US'
        $First.xml | Should -Not -BeNullOrEmpty
        $Result.Sha256 | Should -Match '^[0-9A-F]{64}$'
    }

    It 'uses the metadata projection for high-throughput CSV export' {
        $OutputPath = [System.IO.Path]::Combine($OutputDirectory, 'metadata.csv')

        $Result = Export-EVXEvent `
            -Path $SourcePath `
            -OutputPath $OutputPath `
            -Format Csv `
            -ReadMode Metadata `
            -Oldest `
            -MaxEvents 4

        $Rows = Import-Csv -LiteralPath $OutputPath
        $Result.EventCount | Should -Be 4
        $Rows.Count | Should -Be 4
        $Rows[0].ProviderName | Should -Not -BeNullOrEmpty
        $Rows[0].Message | Should -BeNullOrEmpty
        $Rows[0].Xml | Should -BeNullOrEmpty
    }

    It 'exports extended-length PowerShell character-class paths' {
        $FirstSource = [System.IO.Path]::Combine(
            $OutputDirectory,
            'extended-1.evtx')
        $SecondSource = [System.IO.Path]::Combine(
            $OutputDirectory,
            'extended-2.evtx')
        [System.IO.File]::Copy($SourcePath, $FirstSource)
        [System.IO.File]::Copy($SourcePath, $SecondSource)
        $Pattern = '\\?\' + [System.IO.Path]::Combine(
            $OutputDirectory,
            'extended-[12].evtx')
        $OutputPath = [System.IO.Path]::Combine(
            $OutputDirectory,
            'extended.jsonl')

        $Result = Export-EVXEvent `
            -Path $Pattern `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Metadata `
            -Oldest `
            -MaxEvents 2

        $Result.EventCount | Should -Be 2
        [System.IO.File]::ReadAllLines($OutputPath).Count | Should -Be 2
    }

    It 'does not create output under WhatIf' {
        $OutputPath = [System.IO.Path]::Combine($OutputDirectory, 'whatif.jsonl')

        Export-EVXEvent `
            -Path $SourcePath `
            -OutputPath $OutputPath `
            -WhatIf

        [System.IO.File]::Exists($OutputPath) | Should -BeFalse
    }

    It 'exports a local channel through the same thin command surface' {
        $OutputPath = [System.IO.Path]::Combine($OutputDirectory, 'system.jsonl')

        $Result = Export-EVXEvent `
            -LogName System `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Metadata `
            -MaxEvents 2

        $Lines = [System.IO.File]::ReadAllLines($OutputPath)
        $Result.EventCount | Should -Be 2
        $Lines.Count | Should -Be 2
        ($Lines[0] | ConvertFrom-Json).providerName | Should -Not -BeNullOrEmpty
    }

    It 'can skip the final hash pass for maximum throughput' {
        $OutputPath = [System.IO.Path]::Combine($OutputDirectory, 'no-hash.jsonl')

        $Result = Export-EVXEvent `
            -Path $SourcePath `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Metadata `
            -MaxEvents 2 `
            -SkipHash

        $Result.EventCount | Should -Be 2
        $Result.Sha256 | Should -BeNullOrEmpty
        [System.IO.File]::Exists($OutputPath) | Should -BeTrue
    }

    It 'exports a structured QueryList through the reusable engine' {
        $OutputPath = [System.IO.Path]::Combine(
            $OutputDirectory,
            'structured.jsonl')
        $QueryXml = [EventViewerX.EventFilterCompiler]::BuildFileQueryXml(
            [string[]] @($SourcePath),
            $null,
            $null)

        $Result = Export-EVXEvent `
            -FilterXml $QueryXml `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Metadata `
            -Oldest `
            -MaxEvents 3

        $Result.EventCount | Should -Be 3
        [System.IO.File]::ReadAllLines($OutputPath).Count |
            Should -Be 3
    }

    It 'reports tolerated structured path failures from the pipeline thread' {
        $OutputPath = [System.IO.Path]::Combine(
            $OutputDirectory,
            'tolerated.jsonl')
        $MissingPath = [System.IO.Path]::Combine(
            $OutputDirectory,
            'missing.evtx')
        $EscapedSource = [System.Security.SecurityElement]::Escape(
            $SourcePath)
        $EscapedMissing = [System.Security.SecurityElement]::Escape(
            $MissingPath)
        $QueryXml = @"
<QueryList>
  <Query Id="0" Path="file://$EscapedSource">
    <Select Path="file://$EscapedSource">*</Select>
  </Query>
  <Query Id="1" Path="file://$EscapedMissing">
    <Select Path="file://$EscapedMissing">*</Select>
  </Query>
</QueryList>
"@
        $Errors = @()

        $Result = Export-EVXEvent `
            -FilterXml $QueryXml `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Metadata `
            -TolerateQueryErrors `
            -ErrorAction SilentlyContinue `
            -ErrorVariable Errors

        $Result.EventCount | Should -BeGreaterThan 0
        $Errors | Should -HaveCount 1
        $Errors[0].FullyQualifiedErrorId |
            Should -BeLike 'EVXExportQuerySourceFailed*'
        [System.IO.File]::Exists($OutputPath) | Should -BeTrue
    }

    It 'exports several files as one deterministic bounded stream' {
        $OutputPath = [System.IO.Path]::Combine(
            $OutputDirectory,
            'batch.jsonl')
        $SecondSource = [System.IO.Path]::Combine(
            $PSScriptRoot,
            'Logs',
            'Active Directory Web Services.evtx')

        $Result = Export-EVXEvent `
            -Path $SourcePath, $SecondSource `
            -OutputPath $OutputPath `
            -Format JsonLines `
            -ReadMode Metadata `
            -Oldest `
            -MaxEvents 8

        $Rows = @(
            [System.IO.File]::ReadLines($OutputPath) |
                ForEach-Object { $_ | ConvertFrom-Json }
        )
        $Result.EventCount | Should -Be 8
        $Rows | Should -HaveCount 8
        for ($index = 1; $index -lt $Rows.Count; $index++) {
            [datetime] $Rows[$index].timeCreated |
                Should -BeGreaterOrEqual ([datetime] $Rows[$index - 1].timeCreated)
        }
    }

    It 'rejects archive-resource options for non-EVTX output' {
        $OutputPath = [System.IO.Path]::Combine(
            $OutputDirectory,
            'invalid.jsonl')

        {
            Export-EVXEvent `
                -Path $SourcePath `
                -OutputPath $OutputPath `
                -Format JsonLines `
                -ArchiveResources
        } | Should -Throw
    }
}
