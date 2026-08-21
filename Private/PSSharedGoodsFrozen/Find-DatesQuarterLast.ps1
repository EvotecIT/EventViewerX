function Find-DatesQuarterLast ([bool] $Force) {
    <#
    .SYNOPSIS
    Finds the start and end dates of the last quarter.

    .DESCRIPTION
    This function calculates the start and end dates of the last quarter based on the current date or a specified date.

    .PARAMETER Force
    If set to $true, forces the function to return the last quarter dates even if they were previously retrieved.

    .EXAMPLE
    Find-DatesQuarterLast -Force
    Returns the start and end dates of the last quarter regardless of previous retrieval.

    .EXAMPLE
    Find-DatesQuarterLast -Force $false
    Returns $null if the last quarter dates were already retrieved.

    #>
    #https://blogs.technet.microsoft.com/dsheehan/2017/09/21/use-powershell-to-determine-the-first-day-of-the-current-calendar-quarter/
    $Today = (Get-Date).AddDays(-90)
    $Yesterday = ((Get-Date).AddDays(-1))
    $Quarter = [Math]::Ceiling($Today.Month / 3)
    $LastDay = [DateTime]::DaysInMonth([Int]$Today.Year.ToString(), [Int]($Quarter * 3))
    $StartDate = (get-date -Year $Today.Year -Month ($Quarter * 3 - 2) -Day 1).Date
    $EndDate = (get-date -Year $Today.Year -Month ($Quarter * 3) -Day $LastDay).Date.AddDays(1).AddTicks(-1)

    if ($Force -eq $true -or $Yesterday.Date -eq $EndDate.Date) {
        $DateParameters = @{
            DateFrom = $StartDate
            DateTo   = $EndDate
        }
        return $DateParameters
    } else {
        return $null
    }
}
