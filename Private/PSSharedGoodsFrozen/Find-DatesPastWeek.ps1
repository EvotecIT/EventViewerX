function Find-DatesPastWeek($DayName) {
    <#
    .SYNOPSIS
    Finds the date range for the past week based on the specified day.

    .DESCRIPTION
    This function calculates the date range for the past week based on the specified day of the week.

    .PARAMETER DayName
    The day of the week to use as a reference for finding the past week's date range.

    .EXAMPLE
    Find-DatesPastWeek -DayName "Monday"
    Returns the date range for the past week starting from the previous Monday.

    .EXAMPLE
    Find-DatesPastWeek -DayName "Friday"
    Returns the date range for the past week starting from the previous Friday.

    #>
    $DateTodayStart = Get-Date -Hour 0 -Minute 0 -Second 0 -Millisecond 0
    if ($DateTodayStart.DayOfWeek -ne $DayName) {
        return $null
    }
    $DateTodayEnd = (Get-Date -Hour 0 -Minute 0 -Second 0 -Millisecond 0).AddDays(-7)
    $DateParameters = @{
        DateFrom = $DateTodayEnd
        DateTo   = $DateTodayStart
    }
    return $DateParameters

}
