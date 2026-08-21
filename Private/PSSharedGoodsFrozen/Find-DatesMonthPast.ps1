function Find-DatesMonthPast ([bool] $Force) {
    <#
    .SYNOPSIS
    Finds the dates for the previous month based on the current date.

    .DESCRIPTION
    This function calculates the date range for the previous month based on the current date. It returns the start and end dates of the previous month.

    .PARAMETER Force
    If set to $true, the function will always return the date range for the previous month, regardless of the current date.

    .EXAMPLE
    Find-DatesMonthPast -Force $false
    Returns $null if the current date is not the first day of the month.

    .EXAMPLE
    Find-DatesMonthPast -Force $true
    Returns the date range for the previous month even if the current date is not the first day of the month.
    #>
    $DateToday = (Get-Date).Date
    $DateMonthFirstDay = (GET-DATE -Day 1).Date
    $DateMonthPreviousFirstDay = $DateMonthFirstDay.AddMonths(-1)

    if ($Force -eq $true -or $DateToday -eq $DateMonthFirstDay) {
        $DateParameters = @{
            DateFrom = $DateMonthPreviousFirstDay
            DateTo   = $DateMonthFirstDay
        }
        return $DateParameters
    } else {
        return $null
    }
}
