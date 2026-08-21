function Set-EmailBodyPreparedTable ($TableData, $TableWelcomeMessage) {
    <#
    .SYNOPSIS
    Prepares the email body with a welcome message and table data.

    .DESCRIPTION
    This function prepares the email body by combining a welcome message and table data into a single HTML string.

    .PARAMETER TableData
    The data to be included in the email body table.

    .PARAMETER TableWelcomeMessage
    The welcome message to be displayed at the beginning of the email body.

    .EXAMPLE
    $tableData = "<table><tr><td>John</td><td>Doe</td></tr></table>"
    $welcomeMessage = "Welcome to our platform!"
    Set-EmailBodyPreparedTable -TableData $tableData -TableWelcomeMessage $welcomeMessage

    This example prepares the email body with a welcome message "Welcome to our platform!" and table data "<table><tr><td>John</td><td>Doe</td></tr></table>".

    #>
    $body = "<p><i><u>$TableWelcomeMessage</u></i></p>"
    $body += $TableData
    return $body
}
