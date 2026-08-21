function Start-ADReporting () {
    param (
        [System.Collections.IDictionary]$EmailParameters,
        [System.Collections.IDictionary]$FormattingParameters,
        [System.Collections.IDictionary]$ReportOptions,
        [System.Collections.IDictionary]$ReportTimes,
        [System.Collections.IDictionary]$ReportDefinitions
    )
    Set-DisplayParameters -ReportOptions $ReportOptions

    if ($ReportOptions.DisplayConsole -and $ReportOptions.DisplayConsole.LogFile) {
        $ReportPath = [io.path]::GetDirectoryName($ReportOptions.DisplayConsole.LogFile)
        if (-not [String]::IsNullOrWhiteSpace($ReportPath) -and -not (Test-Path -LiteralPath $ReportPath)) {
            Write-Color -Text '[i] ', "LogFile path doesn't exists ", $ReportPath, ". Please fix log path or provide an empty string. Current log file: ", $ReportOptions.DisplayConsole.LogFile -Color White, White, Yellow, White, Yellow
            return
            #$null = New-Item -Path $ReportPath -ItemType Directory -Force
        }
    }

    Test-Prerequisite $EmailParameters $FormattingParameters $ReportOptions $ReportTimes $ReportDefinitions
    if ($null -ne $ReportOptions.JustTestPrerequisite -and $ReportOptions.JustTestPrerequisite -eq $true) {
        return
    }

    ## Added for compatibility reasons
    if (-not $ReportOptions.Contains('RemoveDuplicates')) {
        $ReportOptions.RemoveDuplicates = $false
    }
    if (-not $ReportOptions.Contains('SendMailOnlyOnEvents')) {
        $ReportOptions.SendMailOnlyOnEvents = $false
    }

    if (-not $ReportDefinitions.ReportsAD.Servers.Contains('UseDirectScan')) {
        if ($ReportOptions.ReportsAD.Servers.UseForwarders) {
            $ReportDefinitions.ReportsAD.Servers.UseDirectScan = $false
        } else {
            $ReportDefinitions.ReportsAD.Servers.UseDirectScan = $true
        }
    }
    if (-not $ReportDefinitions.ReportsAD.Servers.Contains('UseForwarders')) {
        $ReportDefinitions.ReportsAD.Servers.UseDirectScan = $true
    }

    $Dates = Get-ChoosenDates -ReportTimes $ReportTimes
    foreach ($Date in $Dates) {
        Start-Report -Dates $Date -EmailParameters $EmailParameters -FormattingParameters $FormattingParameters -ReportOptions $ReportOptions -ReportDefinitions $ReportDefinitions
    }
}
