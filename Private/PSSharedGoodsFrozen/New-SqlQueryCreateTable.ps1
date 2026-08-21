function New-SqlQueryCreateTable {
    <#
    .SYNOPSIS
    Creates SQL query to generate a new table based on provided table mapping.

    .DESCRIPTION
    This function generates a SQL query to create a new table in a database based on the table mapping provided. The table mapping should be a hashtable where the keys represent the column names and the values represent the column data types and constraints.

    .PARAMETER SqlSettings
    An object containing SQL connection settings.

    .PARAMETER TableMapping
    A hashtable containing the mapping of column names to data types and constraints.

    .EXAMPLE
    $sqlSettings = @{ SqlTable = "MyTable" }
    $tableMapping = @{
        Column1 = "int",
        Column2 = "nvarchar(50) NULL",
        Column3 = "datetime NOT NULL"
    }
    New-SqlQueryCreateTable -SqlSettings $sqlSettings -TableMapping $tableMapping
    #>
    [CmdletBinding()]
    param (
        [Object]$SqlSettings,
        [Object]$TableMapping
    )
    $ArraySQLQueries = New-ArrayList
    $ArrayMain = New-ArrayList
    $ArrayKeys = New-ArrayList

    foreach ($MapKey in $TableMapping.Keys) {
        $MapValue = $TableMapping.$MapKey
        $Field = $MapValue -Split ','
        if ($Field.Count -eq 1) {
            Add-ToArray -List $ArrayKeys -Element "[$($Field[0])] [nvarchar](max) NULL"
        } elseif ($Field.Count -eq 2) {
            Add-ToArray -List $ArrayKeys -Element "[$($Field[0])] $($Field[1]) NULL"
        } elseif ($Field.Count -eq 3) {
            Add-ToArray -List $ArrayKeys -Element "[$($Field[0])] $($Field[1]) $($Field[2])"
        }
    }
    if ($ArrayKeys) {
        Add-ToArray -List $ArrayMain -Element "CREATE TABLE $($SqlSettings.SqlTable) ("
        Add-ToArray -List $ArrayMain -Element "ID int IDENTITY(1,1) PRIMARY KEY,"
        Add-ToArray -List $ArrayMain -Element ($ArrayKeys -join ',')
        Add-ToArray -List $ArrayMain -Element ')'
        Add-ToArray -List $ArraySQLQueries -Element ([string] ($ArrayMain) -replace "`n", "" -replace "`r", "")
    }
    return $ArraySQLQueries
}
