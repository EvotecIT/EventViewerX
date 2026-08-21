Function Test-Prerequisite () {
    param (
        [hashtable] $EmailParameters,
        [hashtable] $FormattingParameters,
        [hashtable] $ReportOptions,
        [hashtable] $ReportTimes,
        [hashtable] $ReportDefinitions
    )
    $Configuration = Test-Configuration $EmailParameters $FormattingParameters $ReportOptions $ReportTimes $ReportDefinitions
    if (-not $Configuration) {
        Write-Color @script:WriteParameters "[i] ", "There are parameters missing in configuration file. Can't continue running...", "Terminated!" -Color White, Yellow, Red
        Exit
    }

    Write-Color @script:WriteParameters "[i] ", "Testing for prerequisite availability..." -Color White, Yellow
    $ImportPSADReporting = Get-ModulesAvailability -Name "PSWinReporting"
    If ($ImportPSADReporting -eq $true) {
        Write-Color @script:WriteParameters  "[+] ", "PSWinReporting", " module imported. Continuing..." -Color White, Green, White
    } else {
        Write-Color @script:WriteParameters  "[-] ", "PSWinReporting", " module not found." -Color White, Red, White
    }

    $ImportExcel = Get-ModulesAvailability -Name "PSWriteExcel"
    if ($ImportExcel -eq $true) {
        Write-Color @script:WriteParameters  "[+] ", "PSWriteExcel", " module imported. Continuing..." -Color White, Green, White
    } else {
        Write-Color @script:WriteParameters  "[-] ", "PSWriteExcel", " module not found." -Color White, Red, White
        if ($ReportOptions.AsExcel -eq $true) {
            Write-Color @script:WriteParameters  "[-] ", "PSWriteExcel ", "module is not installed. Disable ", "AsExcel", " under ", "ReportOptions", " option before rerunning this script." -Color White, Red, White, Yellow, White, Yellow, White
            Write-Color @script:WriteParameters  "[-] ", "Alternatively run ", "Install-Module -Name PSWriteExcel", " before re-running this script. It's quite useful module!" -Color White, White, Yellow, White
            Write-Color @script:WriteParameters  "[-] ", "If ", "Install-Module", " is not there as well (", "poor you - running older system are you?", ") you need to download PackageManagement PowerShell Modules." -Color White, White, Yellow, White, Yellow, White
            Write-Color @script:WriteParameters  "[-] ", "It can be found at ", "https://www.microsoft.com/en-us/download/details.aspx?id=51451", ". After download, install and re-run Install-Module again." -Color White, White, Yellow, White
        }
    }
    $ImportActiveDirectory = Get-ModulesAvailability -Name "ActiveDirectory"
    if ($ImportActiveDirectory -eq $true) {
        Write-Color @script:WriteParameters  "[+] ", "ActiveDirectory", " module imported. Continuing..." -Color White, Green, White
    } else {
        Write-Color @script:WriteParameters  "[-] ", "ActiveDirectory", " module not found." -Color White, Red, White
        Write-Color @script:WriteParameters  "[-] ", "ActiveDirectory", " module is ", "critical", " for operation of this script." -Color White, Red, White, Red, White
        Write-Color @script:WriteParameters  "[-] ", "Please make sure it's available on the machine before running this script" -Color White, Red
    }
    try {
        $TestActiveDirectory = get-addomain
        $AdIsAvailable = $true
    } catch {
        if ($_.Exception -match "Unable to find a default server with Active Directory Web Services running.") {
            Write-Color @script:WriteParameters "[-] ", "Active Directory", " not found. Please run this script with access to ", "Domain Controllers." -Color White, Red, White, Red
        }
        Write-Color @script:WriteParameters "[-] ", "Error: $($_.Exception.Message)" -Color White, Red
        $AdIsAvailable = $false
    }

    if ($ImportPSADReporting -eq $true -and $ImportActiveDirectory -eq $true -and (($ReportOptions.AsExcel -eq $true -and $ImportExcel -eq $true) -or $ReportOptions.AsExcel -eq $false) -and $AdIsAvailable -eq $true) {
        return #$true
    } else {
        Exit
        #return $false
    }
}
