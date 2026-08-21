function Find-DatesDayToday () {
    <#
    .SYNOPSIS
    Finds the start and end dates of the current day.

    .DESCRIPTION
    This function calculates the start and end dates of the current day based on the current date.
    #>
    $DateToday = (GET-DATE).Date
    $DateTodayEnd = $DateToday.AddDays(1).AddSeconds(-1)

    $DateParameters = @{
        DateFrom = $DateToday
        DateTo   = $DateTodayEnd
    }
    return $DateParameters
}
