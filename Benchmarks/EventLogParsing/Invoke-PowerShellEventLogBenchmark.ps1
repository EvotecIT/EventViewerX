param(
    [Parameter(Mandatory)]
    [ValidateSet('PSEventViewer', 'GetWinEvent')]
    [string] $Engine,

    [Parameter(Mandatory)]
    [string] $Path,

    [Parameter(Mandatory)]
    [ValidateSet('Metadata', 'Message', 'StructuredData', 'StructuredDataAndMessage', 'Full')]
    [string] $ReadMode,

    [Parameter(Mandatory)]
    [string] $ResultPath,

    [string] $ModulePath,

    [string] $CsvOutputPath,

    [string] $XmlOutputPath,

    [ValidateRange(0, [int]::MaxValue)]
    [int] $MaxEvents,

    [Globalization.CultureInfo] $MessageCulture = [Globalization.CultureInfo]::GetCultureInfo('en-US')
)

$ErrorActionPreference = 'Stop'
[Globalization.CultureInfo]::CurrentUICulture = $MessageCulture
[Globalization.CultureInfo]::DefaultThreadCurrentUICulture = $MessageCulture

if ($Engine -eq 'PSEventViewer') {
    if (-not $ModulePath) {
        throw 'ModulePath is required for the PSEventViewer engine.'
    }
    Import-Module -Name $ModulePath -Force -ErrorAction Stop
}

function Measure-EventPipeline {
    <#
    .SYNOPSIS
    Consumes event records without accumulating them and records equivalent projection metrics.

    .DESCRIPTION
    Uses a pipeline process block so the benchmark includes normal PowerShell streaming overhead
    without adding ForEach-Object or retaining a million event objects in memory.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [object] $InputObject,

        [Parameter(Mandatory)]
        [ValidateSet('PSEventViewer', 'GetWinEvent')]
        [string] $InputEngine,

        [Parameter(Mandatory)]
        [ValidateSet('Metadata', 'Message', 'StructuredData', 'StructuredDataAndMessage', 'Full')]
        [string] $Mode,

        [switch] $PassThru
    )

    begin {
        [long] $count = 0
        [long] $idSum = 0
        [long] $recordIdSum = 0
        [long] $timeTicksXor = 0
        [long] $orderSignature = 0
        [long] $orderModulus = 1000000007
        [long] $orderMultiplier = 1000003
        [long] $metadataTouch = 0
        [long] $messageCharacters = 0
        [long] $xmlCharacters = 0
        [long] $propertyCount = 0
        [long] $structuredFieldCount = 0
        [long] $messageFieldCount = 0
        [long] $attachmentBytes = 0
        [Nullable[long]] $firstRecordId = $null
        [Nullable[long]] $lastRecordId = $null
    }

    process {
        $eventRecord = $InputObject
        $count++
        $idSum += [long] $eventRecord.Id
        [long] $recordIdForOrder = 0

        if ($null -ne $eventRecord.RecordId) {
            [long] $recordId = $eventRecord.RecordId
            $recordIdForOrder = $recordId
            $recordIdSum += $recordId
            if ($null -eq $firstRecordId) {
                $firstRecordId = $recordId
            }
            $lastRecordId = $recordId
        }

        [long] $timeTicksForOrder = 0
        if ($null -ne $eventRecord.TimeCreated) {
            $timeTicksForOrder = [long] $eventRecord.TimeCreated.Ticks
            $timeTicksXor = $timeTicksXor -bxor $timeTicksForOrder
        }
        $orderSignature = (
            ($orderSignature * $orderMultiplier) +
            ($recordIdForOrder % $orderModulus) +
            (([long] $eventRecord.Id % $orderModulus) * 31) +
            (($timeTicksForOrder % $orderModulus) * 17)
        ) % $orderModulus

        foreach ($text in $eventRecord.ProviderName, $eventRecord.MachineName, $eventRecord.LogName) {
            if ($null -ne $text) {
                $metadataTouch += ([string] $text).Length
            }
        }

        foreach ($number in $eventRecord.Level, $eventRecord.Keywords, $eventRecord.Task,
                 $eventRecord.Opcode, $eventRecord.ProcessId, $eventRecord.ThreadId) {
            if ($null -ne $number) {
                $metadataTouch++
            }
        }

        if ($Mode -in 'Message', 'StructuredDataAndMessage', 'Full') {
            $message = [string] $eventRecord.Message
            $messageCharacters += $message.Length
            foreach ($displayName in $eventRecord.LevelDisplayName, $eventRecord.TaskDisplayName,
                     $eventRecord.OpcodeDisplayName) {
                if ($null -ne $displayName) {
                    $metadataTouch += ([string] $displayName).Length
                }
            }
            if ($null -ne $eventRecord.KeywordsDisplayNames) {
                $metadataTouch += @($eventRecord.KeywordsDisplayNames).Count
            }
        }

        if ($Mode -in 'StructuredData', 'StructuredDataAndMessage', 'Full') {
            if ($null -ne $eventRecord.Properties) {
                $propertyCount += $eventRecord.Properties.Count
            }

            if ($InputEngine -eq 'PSEventViewer') {
                $xml = [string] $eventRecord.XMLData
                $xmlCharacters += $xml.Length
                $structuredFieldCount += $eventRecord.Data.Count
                if ($Mode -eq 'Full') {
                    $messageFieldCount += $eventRecord.MessageData.Count
                }
                foreach ($attachment in $eventRecord.Attachments) {
                    $attachmentBytes += $attachment.LongLength
                }
            } else {
                $xml = [string] $eventRecord.ToXml()
                $xmlCharacters += $xml.Length
            }
        }

        if ($PassThru) {
            $InputObject
        }
    }

    end {
        $metrics = [pscustomobject] @{
            Count                = $count
            IdSum                = $idSum
            RecordIdSum          = $recordIdSum
            TimeTicksXor         = $timeTicksXor
            OrderSignature       = $orderSignature
            FirstRecordId        = $firstRecordId
            LastRecordId         = $lastRecordId
            MetadataTouch        = $metadataTouch
            MessageCharacters    = $messageCharacters
            XmlCharacters        = $xmlCharacters
            PropertyCount        = $propertyCount
            StructuredFieldCount = $structuredFieldCount
            MessageFieldCount    = $messageFieldCount
            AttachmentBytes      = $attachmentBytes
        }
        if ($PassThru) {
            $script:projection = $metrics
        } else {
            $metrics
        }
    }
}

[GC]::Collect()
[GC]::WaitForPendingFinalizers()
[GC]::Collect()
[long] $allocatedBefore = [GC]::GetTotalAllocatedBytes($false)
[int] $gen0Before = [GC]::CollectionCount(0)
[int] $gen1Before = [GC]::CollectionCount(1)
[int] $gen2Before = [GC]::CollectionCount(2)
$stopwatch = [Diagnostics.Stopwatch]::StartNew()

$csvFullPath = if ($CsvOutputPath) {
    [IO.Path]::GetFullPath($CsvOutputPath)
} else {
    $null
}
if ($csvFullPath) {
    $csvDirectory = Split-Path -Parent $csvFullPath
    New-Item -ItemType Directory -Force -Path $csvDirectory | Out-Null
}
$xmlFullPath = if ($XmlOutputPath) {
    [IO.Path]::GetFullPath($XmlOutputPath)
} else {
    $null
}
if ($xmlFullPath) {
    $xmlDirectory = Split-Path -Parent $xmlFullPath
    New-Item -ItemType Directory -Force -Path $xmlDirectory | Out-Null
}
if ($csvFullPath -and $xmlFullPath) {
    throw 'CsvOutputPath and XmlOutputPath are mutually exclusive.'
}

if ($Engine -eq 'PSEventViewer') {
    $parameters = @{
        Path           = $Path
        ReadMode       = $ReadMode
        Oldest         = $true
        MessageCulture = $MessageCulture
    }
    if ($MaxEvents -gt 0) {
        $parameters.MaxEvents = $MaxEvents
    }
    if ($xmlFullPath) {
        $exportResult = Export-EVXEvent `
            -Path $Path `
            -OutputPath $xmlFullPath `
            -Format Xml `
            -ReadMode StructuredData `
            -Oldest `
            -MaxEvents $MaxEvents `
            -MessageCulture $MessageCulture `
            -SkipHash `
            -Force
        $projection = [pscustomobject] @{
            Count                = [long] $exportResult.EventCount
            IdSum                = 0
            RecordIdSum          = 0
            TimeTicksXor         = 0
            OrderSignature       = 0
            FirstRecordId        = $null
            LastRecordId         = $null
            MetadataTouch        = 0
            MessageCharacters    = 0
            XmlCharacters        = 0
            PropertyCount        = 0
            StructuredFieldCount = 0
            MessageFieldCount    = 0
            AttachmentBytes      = 0
        }
    } elseif ($csvFullPath) {
        $script:projection = $null
        Get-EVXEvent @parameters |
            Measure-EventPipeline -InputEngine PSEventViewer -Mode $ReadMode -PassThru |
            Select-Object TimeCreated, RecordId, Id, ProviderName, MachineName |
            Export-Csv -LiteralPath $csvFullPath -NoTypeInformation
        $projection = $script:projection
    } else {
        $projection = Get-EVXEvent @parameters |
            Measure-EventPipeline -InputEngine PSEventViewer -Mode $ReadMode
    }
} else {
    $parameters = @{
        Path   = $Path
        Oldest = $true
    }
    if ($MaxEvents -gt 0) {
        $parameters.MaxEvents = $MaxEvents
    }
    if ($xmlFullPath) {
        $settings = [Xml.XmlWriterSettings]::new()
        $settings.Encoding = [Text.UTF8Encoding]::new($false)
        $settings.Indent = $false
        $settings.CloseOutput = $false
        $settings.NewLineHandling = [Xml.NewLineHandling]::None
        $stream = [IO.FileStream]::new(
            $xmlFullPath,
            [IO.FileMode]::Create,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None,
            1MB,
            [IO.FileOptions]::SequentialScan
        )
        $writer = [Xml.XmlWriter]::Create($stream, $settings)
        [long] $script:xmlEventCount = 0
        try {
            $writer.WriteStartDocument()
            $writer.WriteStartElement('Events')
            Get-WinEvent @parameters | & {
                process {
                    $writer.WriteRaw($_.ToXml())
                    $script:xmlEventCount++
                }
            }
            $writer.WriteEndElement()
            $writer.WriteEndDocument()
            $writer.Flush()
            $stream.Flush($true)
        } finally {
            $writer.Dispose()
            $stream.Dispose()
        }
        $projection = [pscustomobject] @{
            Count                = $script:xmlEventCount
            IdSum                = 0
            RecordIdSum          = 0
            TimeTicksXor         = 0
            OrderSignature       = 0
            FirstRecordId        = $null
            LastRecordId         = $null
            MetadataTouch        = 0
            MessageCharacters    = 0
            XmlCharacters        = 0
            PropertyCount        = 0
            StructuredFieldCount = 0
            MessageFieldCount    = 0
            AttachmentBytes      = 0
        }
    } elseif ($csvFullPath) {
        $script:projection = $null
        Get-WinEvent @parameters |
            Measure-EventPipeline -InputEngine GetWinEvent -Mode $ReadMode -PassThru |
            Select-Object TimeCreated, RecordId, Id, ProviderName, MachineName |
            Export-Csv -LiteralPath $csvFullPath -NoTypeInformation
        $projection = $script:projection
    } else {
        $projection = Get-WinEvent @parameters |
            Measure-EventPipeline -InputEngine GetWinEvent -Mode $ReadMode
    }
}

$stopwatch.Stop()
$process = [Diagnostics.Process]::GetCurrentProcess()
$result = [ordered] @{
    Engine               = $Engine
    ReadMode             = $ReadMode
    FixturePath          = [IO.Path]::GetFullPath($Path)
    RuntimeVersion       = [Environment]::Version.ToString()
    ProductVersion       = if ($Engine -eq 'PSEventViewer') {
        [Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($ModulePath)).ProductVersion
    } else {
        $PSVersionTable.PSVersion.ToString()
    }
    Count                = $projection.Count
    IdSum                = $projection.IdSum
    RecordIdSum          = $projection.RecordIdSum
    TimeTicksXor         = $projection.TimeTicksXor
    OrderSignature       = $projection.OrderSignature
    FirstRecordId        = $projection.FirstRecordId
    LastRecordId         = $projection.LastRecordId
    MetadataTouch        = $projection.MetadataTouch
    MessageCharacters    = $projection.MessageCharacters
    XmlCharacters        = $projection.XmlCharacters
    PropertyCount        = $projection.PropertyCount
    StructuredFieldCount = $projection.StructuredFieldCount
    MessageFieldCount    = $projection.MessageFieldCount
    AttachmentBytes      = $projection.AttachmentBytes
    AllocatedBytes       = [GC]::GetTotalAllocatedBytes($false) - $allocatedBefore
    PeakWorkingSetBytes  = $process.PeakWorkingSet64
    Gen0Collections      = [GC]::CollectionCount(0) - $gen0Before
    Gen1Collections      = [GC]::CollectionCount(1) - $gen1Before
    Gen2Collections      = [GC]::CollectionCount(2) - $gen2Before
    ElapsedMilliseconds  = $stopwatch.Elapsed.TotalMilliseconds
    OutputPath           = if ($csvFullPath) { $csvFullPath } else { $xmlFullPath }
    OutputBytes          = if ($xmlFullPath) { (Get-Item -LiteralPath $xmlFullPath).Length } else { 0 }
    # PowerForge hashes every retained output in the post-operation validator.
    # Keeping this null prevents integrity validation from contaminating the
    # engine timing for the PowerShell-hosted runners.
    OutputSha256         = $null
}

$resultDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ResultPath))
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding utf8
