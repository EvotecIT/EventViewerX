function Find-DatesCurrentHour () {
    <#
    .SYNOPSIS
    Finds the start and end dates for the current hour.

    .DESCRIPTION
    This function calculates the start and end dates for the current hour.

    .EXAMPLE
    PS C:\> Find-DatesCurrentHour
    DateFrom                     DateTo
    --------                     ------
    10/20/2021 12:00:00 AM       10/20/2021 1:00:00 AM
    #>
    $DateTodayStart = (Get-Date -Minute 0 -Second 0 -Millisecond 0)
    $DateTodayEnd = $DateTodayStart.AddHours(1)

    $DateParameters = @{
        DateFrom = $DateTodayStart
        DateTo   = $DateTodayEnd
    }
    return $DateParameters
}
