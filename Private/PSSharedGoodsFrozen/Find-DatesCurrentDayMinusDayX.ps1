function Find-DatesCurrentDayMinusDayX ($days) {
    <#
    .SYNOPSIS
    Finds the date range for the current day minus a specified number of days.

    .DESCRIPTION
    This function calculates the start and end dates for the current day minus a specified number of days.

    .PARAMETER days
    Specifies the number of days to subtract from the current day.

    .EXAMPLE
    Find-DatesCurrentDayMinusDayX -days 1
    Returns the date range for yesterday.

    .EXAMPLE
    Find-DatesCurrentDayMinusDayX -days 7
    Returns the date range for a week ago.

    #>
    $DateTodayStart = (Get-Date -Hour 0 -Minute 0 -Second 0 -Millisecond 0).AddDays( - $Days)
    $DateTodayEnd = (Get-Date -Hour 0 -Minute 0 -Second 0 -Millisecond 0).AddDays(1).AddDays( - $Days).AddMilliseconds(-1)

    $DateParameters = @{
        DateFrom = $DateTodayStart
        DateTo   = $DateTodayEnd
    }
    return $DateParameters
}
