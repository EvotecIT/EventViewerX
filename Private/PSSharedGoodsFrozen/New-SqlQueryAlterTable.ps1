function New-SqlQueryAlterTable {
    <#
    .SYNOPSIS
    Creates SQL queries to add new columns to an existing table.

    .DESCRIPTION
    This function generates SQL queries to add new columns to an existing SQL table based on the provided TableMapping and ExistingColumns.

    .PARAMETER SqlSettings
    An object containing SQL connection settings.

    .PARAMETER TableMapping
    An object representing the mapping of new columns to be added. Keys are column names, values are column definitions.

    .PARAMETER ExistingColumns
    An array of existing column names in the table.

    .EXAMPLE
    $sqlSettings = Get-SqlSettings
    $tableMapping = @{
        "NewColumn1" = "Column1Name, nvarchar(50)"
        "NewColumn2" = "Column2Name, int"
    }
    $existingColumns = @("Column1Name", "Column3Name")

    New-SqlQueryAlterTable -SqlSettings $sqlSettings -TableMapping $tableMapping -ExistingColumns $existingColumns
    # Generates SQL queries to add "NewColumn1" and "NewColumn2" to the table.

    #>
    [CmdletBinding()]
    param (
        [Object]$SqlSettings,
        [Object]$TableMapping,
        [string[]] $ExistingColumns
    )
    $ArraySQLQueries = New-ArrayList
    $ArrayMain = New-ArrayList
    $ArrayKeys = New-ArrayList

    foreach ($MapKey in $TableMapping.Keys) {
        $MapValue = $TableMapping.$MapKey
        $Field = $MapValue -Split ','


        if ($ExistingColumns -notcontains $MapKey -and $ExistingColumns -notcontains $Field[0]) {
            if ($Field.Count -eq 1) {
                Add-ToArray -List $ArrayKeys -Element "[$($Field[0])] [nvarchar](max) NULL"
            } elseif ($Field.Count -eq 2) {
                Add-ToArray -List $ArrayKeys -Element "[$($Field[0])] $($Field[1]) NULL"
            } elseif ($Field.Count -eq 3) {
                Add-ToArray -List $ArrayKeys -Element "[$($Field[0])] $($Field[1]) $($Field[2])"
            }
        }
    }

    if ($ArrayKeys) {
        Add-ToArray -List $ArrayMain -Element "ALTER TABLE $($SqlSettings.SqlTable) ADD"
        Add-ToArray -List $ArrayMain -Element ($ArrayKeys -join ',')
        Add-ToArray -List $ArrayMain -Element ';'
        Add-ToArray -List $ArraySQLQueries -Element ([string] ($ArrayMain) -replace "`n", "" -replace "`r", "")
    }
    return $ArraySQLQueries
}
