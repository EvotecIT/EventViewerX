function Add-ToHashTable($Hashtable, $Key, $Value) {
    <#
    .SYNOPSIS
    Adds a key-value pair to a hashtable.

    .DESCRIPTION
    This function adds a key-value pair to a given hashtable. If the value is not null or empty, it is added to the hashtable.

    .PARAMETER Hashtable
    The hashtable to which the key-value pair will be added.

    .PARAMETER Key
    The key of the key-value pair to be added.

    .PARAMETER Value
    The value of the key-value pair to be added.

    .EXAMPLE
    $myHashtable = @{}
    Add-ToHashTable -Hashtable $myHashtable -Key "Name" -Value "John"
    # Adds the key-value pair "Name"-"John" to $myHashtable.

    .EXAMPLE
    $myHashtable = @{}
    Add-ToHashTable -Hashtable $myHashtable -Key "Age" -Value 25
    # Adds the key-value pair "Age"-25 to $myHashtable.
    #>
    if ($null -ne $Value -and
        -not ($Value -is [string] -and $Value.Length -eq 0) -and
        -not ($Value -is [System.Collections.ICollection] -and $Value.Count -eq 0)) {
        $Hashtable.Add($Key, $Value)
    }
}
