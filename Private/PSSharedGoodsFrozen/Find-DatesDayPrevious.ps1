function Find-DatesDayPrevious () {
    <#
    .SYNOPSIS
    Finds the date parameters for the previous day.

    .DESCRIPTION
    This function calculates the date parameters for the previous day based on the current date.

    .EXAMPLE
    Find-DatesDayPrevious
    Returns the date parameters for the previous day.

    #>
    $DateToday = (GET-DATE).Date
    $DateYesterday = $DateToday.AddDays(-1)

    $DateParameters = @{
        DateFrom = $DateYesterday
        DateTo   = $dateToday
    }
    return $DateParameters
}
