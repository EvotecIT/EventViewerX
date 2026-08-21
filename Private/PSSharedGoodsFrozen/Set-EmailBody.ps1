function Set-EmailBody {
    <#
    .SYNOPSIS
    Sets the email body content with a welcome message and table data.

    .DESCRIPTION
    This function sets the email body content by combining a welcome message and table data into a single HTML string. If there is no data in the table, a default message is displayed.

    .PARAMETER TableData
    The data to be included in the email body table.

    .PARAMETER TableMessageWelcome
    The welcome message to be displayed at the beginning of the email body.

    .PARAMETER TableMessageNoData
    The message to be displayed when there is no data in the table.

    .EXAMPLE
    $tableData = @("Name", "Age", "Location"), @("Alice", "30", "New York"), @("Bob", "25", "Los Angeles")
    $welcomeMessage = "Welcome to our platform!"
    Set-EmailBody -TableData $tableData -TableMessageWelcome $welcomeMessage

    This example sets the email body with a welcome message "Welcome to our platform!" and table data. If there is no data in the table, the default message "No changes happened during that period." is displayed.

    #>
    [CmdletBinding()]
    param(
        [Object] $TableData,
        [alias('TableWelcomeMessage')][string] $TableMessageWelcome,
        [string] $TableMessageNoData = 'No changes happened during that period.'
    )
    $Body = "<p><i><u>$TableMessageWelcome</u></i></p>"
    if ($($TableData | Measure-Object).Count -gt 0) {
        $Body += $TableData | ConvertTo-Html -Fragment | Out-String
        # $Body += "</p>"
    } else {
        $Body += "<p><i>$TableMessageNoData</i></p>"
    }
    return $body
}
