function New-ArrayList {
    <#
    .SYNOPSIS
    Creates a new ArrayList object.

    .DESCRIPTION
    This function creates a new instance of the ArrayList class from the System.Collections namespace.

    .EXAMPLE
    $myList = New-ArrayList
    $myList.Add("Apple")
    $myList.Add("Banana")
    $myList.Add("Orange")
    $myList
    #>
    [CmdletBinding()]
    param()
    $List = [System.Collections.ArrayList]::new()
    <#
    Mathias R�rbo Jessen:
        The pipeline will attempt to unravel the list on assignment,
        so you'll have to either wrap the empty arraylist in an array,
        like above, or call WriteObject explicitly and tell it not to, like so:
        $PSCmdlet.WriteObject($List,$false)
    #>
    return , $List
}
