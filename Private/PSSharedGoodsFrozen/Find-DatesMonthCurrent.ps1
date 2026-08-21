function Find-DatesMonthCurrent () {
    <#
    .SYNOPSIS
    Finds the start and end dates of the current month.

    .DESCRIPTION
    This function calculates the start and end dates of the current month based on the current date.

    .EXAMPLE
    Find-DatesMonthCurrent
    Returns the start and end dates of the current month.

    #>
    $DateMonthFirstDay = (GET-DATE -Day 1).Date
    $DateMonthLastDay = GET-DATE $DateMonthFirstDay.AddMonths(1).AddSeconds(-1)

    $DateParameters = @{
        DateFrom = $DateMonthFirstDay
        DateTo   = $DateMonthLastDay
    }
    return $DateParameters
}
