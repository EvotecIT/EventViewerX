function Find-DatesQuarterCurrent ([bool] $Force) {
    <#
    .SYNOPSIS
    Finds the start and end dates of the current quarter.

    .DESCRIPTION
    This function calculates the start and end dates of the current quarter based on the current date.

    .PARAMETER Force
    If set to $true, forces the function to recalculate the dates even if they have been previously calculated.

    .EXAMPLE
    Find-DatesQuarterCurrent -Force $false
    Returns the start and end dates of the current quarter without recalculating if already calculated.

    .EXAMPLE
    Find-DatesQuarterCurrent -Force $true
    Forces the function to recalculate and returns the start and end dates of the current quarter.

    #>
    $Today = (Get-Date)
    $Quarter = [Math]::Ceiling($Today.Month / 3)
    $LastDay = [DateTime]::DaysInMonth([Int]$Today.Year.ToString(), [Int]($Quarter * 3))
    $StartDate = (get-date -Year $Today.Year -Month ($Quarter * 3 - 2) -Day 1).Date
    $EndDate = (get-date -Year $Today.Year -Month ($Quarter * 3) -Day $LastDay).Date.AddDays(1).AddTicks(-1)
    $DateParameters = @{
        DateFrom = $StartDate
        DateTo   = $EndDate
    }
    return $DateParameters
}
