function Get-HTML {
    <#
    .SYNOPSIS
    Splits the input text by carriage return and outputs each line.

    .DESCRIPTION
    This function takes a string input and splits it by carriage return (`r) to output each line separately.

    .PARAMETER text
    The input text to be split and displayed line by line.

    .EXAMPLE
    Get-HTML -text "Line 1`rLine 2`rLine 3"
    This example splits the input text by carriage return and outputs each line separately.

    #>
    [CmdletBinding()]
    param (
        [string] $text
    )
    $text = $text.Split("`r")
    foreach ($t in $text) {
        Write-Host $t
    }
}
