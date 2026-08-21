function Add-ToHashTable($Hashtable, $Key, $Value) {
    if ($null -ne $Value -and
        -not ($Value -is [string] -and $Value.Length -eq 0) -and
        -not ($Value -is [System.Collections.ICollection] -and $Value.Count -eq 0)) {
        $Hashtable.Add($Key, $Value)
    }
}
