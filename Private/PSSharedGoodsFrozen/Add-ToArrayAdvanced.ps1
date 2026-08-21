function Add-ToArrayAdvanced {
    <#
    .SYNOPSIS
    Adds an element to an ArrayList with advanced options.

    .DESCRIPTION
    The Add-ToArrayAdvanced function adds an element to an ArrayList with various options such as skipping null elements, requiring uniqueness, performing full comparison, and merging elements.

    .PARAMETER List
    The ArrayList to which the element will be added.

    .PARAMETER Element
    The element to be added to the ArrayList.

    .PARAMETER SkipNull
    If specified, skips adding null elements to the ArrayList.

    .PARAMETER RequireUnique
    If specified, ensures that the element is unique in the ArrayList.

    .PARAMETER FullComparison
    If specified with RequireUnique, performs a full comparison of elements before adding.

    .PARAMETER Merge
    If specified, merges the element into the ArrayList.

    .EXAMPLE
    Add-ToArrayAdvanced -List $myList -Element "Apple"

    Description:
    Adds the string "Apple" to the ArrayList $myList.

    .EXAMPLE
    Add-ToArrayAdvanced -List $myList -Element "Banana" -RequireUnique -FullComparison

    Description:
    Adds the string "Banana" to the ArrayList $myList only if it is not already present, performing a full comparison.

    #>
    [CmdletBinding()]
    param(
        [System.Collections.ArrayList] $List,
        [Object] $Element,
        [switch] $SkipNull,
        [switch] $RequireUnique,
        [switch] $FullComparison,
        [switch] $Merge
    )
    if ($SkipNull -and $null -eq $Element) {
        #Write-Verbose "Add-ToArrayAdvanced - SkipNull used"
        return
    }
    if ($RequireUnique) {
        if ($FullComparison) {
            foreach ($ListElement in $List) {
                if ($ListElement -eq $Element) {
                    $TypeLeft = Get-ObjectType -Object $ListElement
                    $TypeRight = Get-ObjectType -Object $Element
                    if ($TypeLeft.ObjectTypeName -eq $TypeRight.ObjectTypeName) {
                        #Write-Verbose "Add-ToArrayAdvanced - RequireUnique with full comparison used"
                        return
                    }
                }
            }
        } else {
            if ($List -contains $Element) {
                #Write-Verbose "Add-ToArrayAdvanced - RequireUnique on name used"
                return
            }
        }
    }
    #Write-Verbose "Add-ToArrayAdvanced - Adding ELEMENT: $Element"
    if ($Merge) {
        [void] $List.AddRange($Element) # > $null
    } else {
        [void] $List.Add($Element) # > $null
    }
}
