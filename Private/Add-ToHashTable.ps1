function Add-ToHashTable($Hashtable, $Key, $Value) {
    if ($null -ne $Value -and $Value -ne '') {
        $Hashtable.Add($Key, $Value)
    }
}
