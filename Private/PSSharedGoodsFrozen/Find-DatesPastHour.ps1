function Find-DatesPastHour () {
    <#
    .SYNOPSIS
    Finds the date range for the past hour.

    .DESCRIPTION
    This function calculates the date range for the past hour, starting from the beginning of the previous hour up to the current hour.

    .EXAMPLE
    Find-DatesPastHour
    Returns a hashtable with DateFrom and DateTo keys representing the date range for the past hour.

    #>
    $DateTodayEnd = Get-Date -Minute 0 -Second 0 -Millisecond 0
    $DateTodayStart = $DateTodayEnd.AddHours(-1)

    $DateParameters = @{
        DateFrom = $DateTodayStart
        DateTo   = $DateTodayEnd
    }
    return $DateParameters
}
