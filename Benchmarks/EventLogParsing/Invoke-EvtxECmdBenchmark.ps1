param(
    [Parameter(Mandatory)]
    [string] $ExecutablePath,

    [Parameter(Mandatory)]
    [string] $Path,

    [Parameter(Mandatory)]
    [string] $MapsPath,

    [Parameter(Mandatory)]
    [string] $ResultPath,

    [Parameter(Mandatory)]
    [string] $StandardOutputPath,

    [ValidateSet('NativeParse', 'ForensicCsv', 'FullJson', 'Xml')]
    [string] $Workload = 'NativeParse',

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = [IO.Path]::GetFullPath($ExecutablePath)
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.ArgumentList.Add('-f')
$startInfo.ArgumentList.Add([IO.Path]::GetFullPath($Path))
$startInfo.ArgumentList.Add('--met')
$mapsFullPath = [IO.Path]::GetFullPath($MapsPath)
if (-not (Test-Path -LiteralPath $mapsFullPath -PathType Container)) {
    throw "EvtxECmd maps directory '$mapsFullPath' does not exist."
}
$startInfo.ArgumentList.Add('--maps')
$startInfo.ArgumentList.Add($mapsFullPath)

$outputFullPath = if ($OutputPath) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    $null
}
if ($Workload -ne 'NativeParse' -and -not $outputFullPath) {
    throw "OutputPath is required for the EvtxECmd workload '$Workload'."
}
if ($outputFullPath) {
    $outputDirectory = Split-Path -Parent $outputFullPath
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    switch ($Workload) {
        'ForensicCsv' {
            $startInfo.ArgumentList.Add('--csv')
            $startInfo.ArgumentList.Add($outputDirectory)
            $startInfo.ArgumentList.Add('--csvf')
            $startInfo.ArgumentList.Add((Split-Path -Leaf $outputFullPath))
        }
        'FullJson' {
            $startInfo.ArgumentList.Add('--json')
            $startInfo.ArgumentList.Add($outputDirectory)
            $startInfo.ArgumentList.Add('--jsonf')
            $startInfo.ArgumentList.Add((Split-Path -Leaf $outputFullPath))
            $startInfo.ArgumentList.Add('--fj')
            $startInfo.ArgumentList.Add('true')
        }
        'Xml' {
            $startInfo.ArgumentList.Add('--xml')
            $startInfo.ArgumentList.Add($outputDirectory)
            $startInfo.ArgumentList.Add('--xmlf')
            $startInfo.ArgumentList.Add((Split-Path -Leaf $outputFullPath))
        }
    }
}

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$process = [Diagnostics.Process]::Start($startInfo)
$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()
[long] $peakWorkingSet = 0
while (-not $process.WaitForExit(25)) {
    $process.Refresh()
    if ($process.WorkingSet64 -gt $peakWorkingSet) {
        $peakWorkingSet = $process.WorkingSet64
    }
}
$process.WaitForExit()
$process.Refresh()
$peakWorkingSet = [Math]::Max(
    $peakWorkingSet,
    [Math]::Max([long] $process.PeakWorkingSet64, [long] $process.WorkingSet64)
)
$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
$stopwatch.Stop()

$exitCode = $process.ExitCode
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    [IO.Path]::GetFullPath($ExecutablePath)
).ProductVersion
$process.Dispose()

$outputDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($StandardOutputPath))
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$stdout | Set-Content -LiteralPath $StandardOutputPath -Encoding utf8
if ($stderr) {
    $stderr | Add-Content -LiteralPath $StandardOutputPath -Encoding utf8
}

# Some redirected EvtxECmd builds emit the locale thousands separator through
# a legacy code page. The .NET reader replaces an undecodable separator with
# U+FFFD; it is still safe here because the match is anchored to a metrics label
# and every non-digit in the captured integer is removed below.
$groupedIntegerPattern = '[0-9][0-9,._\p{Zs}\uFFFD ]*'
$totalMatch = [regex]::Match($stdout, "Total event log records found:\s*($groupedIntegerPattern)")
$includedMatch = [regex]::Match($stdout, "Records included:\s*($groupedIntegerPattern)")
$errorMatch = [regex]::Match($stdout, "Errors:\s*($groupedIntegerPattern)")
if (-not $totalMatch.Success -or -not $includedMatch.Success) {
    throw 'EvtxECmd output did not contain the expected record metrics.'
}
if ($exitCode -ne 0) {
    throw "EvtxECmd exited with code $exitCode. See '$StandardOutputPath'."
}
if ($outputFullPath -and -not (Test-Path -LiteralPath $outputFullPath -PathType Leaf)) {
    throw "EvtxECmd did not create the expected $Workload output '$outputFullPath'."
}

$totalRecords = [long] ($totalMatch.Groups[1].Value -replace '[^0-9]', '')
$includedRecords = [long] ($includedMatch.Groups[1].Value -replace '[^0-9]', '')
$errors = if ($errorMatch.Success) {
    [long] ($errorMatch.Groups[1].Value -replace '[^0-9]', '')
} else {
    0
}

$result = [ordered] @{
    Engine               = 'EvtxECmd'
    ReadMode             = $Workload
    FixturePath          = [IO.Path]::GetFullPath($Path)
    RuntimeVersion       = [Environment]::Version.ToString()
    ProductVersion       = $productVersion
    Count                = $includedRecords
    TotalRecords         = $totalRecords
    Errors               = $errors
    AllocatedBytes       = 0
    PeakWorkingSetBytes  = $peakWorkingSet
    ElapsedMilliseconds  = $stopwatch.Elapsed.TotalMilliseconds
    StandardOutputBytes  = (Get-Item -LiteralPath $StandardOutputPath).Length
    OutputPath           = $outputFullPath
    OutputBytes          = 0
    OutputSha256         = $null
}

$resultDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ResultPath))
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding utf8
