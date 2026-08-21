function Add-ToArray {
    <#
    .SYNOPSIS
    Adds an element to an ArrayList.

    .DESCRIPTION
    This function adds an element to the specified ArrayList.

    .PARAMETER List
    The ArrayList to which the element will be added.

    .PARAMETER Element
    The element to be added to the ArrayList.

    .EXAMPLE
    $myList = New-Object System.Collections.ArrayList
    Add-ToArray -List $myList -Element "Apple"
    # Adds the string "Apple" to the ArrayList $myList.

    .EXAMPLE
    $myList = New-Object System.Collections.ArrayList
    Add-ToArray -List $myList -Element 42
    # Adds the integer 42 to the ArrayList $myList.
    #>
    [CmdletBinding()]
    param(
        [System.Collections.ArrayList] $List,
        [Object] $Element
    )
    #Write-Verbose "Add-ToArray - Element: $Element"
    [void] $List.Add($Element) #> $null
}
