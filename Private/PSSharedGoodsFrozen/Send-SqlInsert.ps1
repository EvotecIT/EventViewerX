function Send-SqlInsert {
    <#
    .SYNOPSIS
    Send data to a SQL table with optional table creation and alteration capabilities.

    .DESCRIPTION
    This function sends data to a specified SQL table. It provides options for table creation and alteration based on the provided settings.

    .PARAMETER Object
    Array of objects to be inserted into the SQL table.

    .PARAMETER SqlSettings
    Dictionary containing SQL server, database, and table information along with optional settings for table operations.

    .EXAMPLE
    Send-SqlInsert -Object $DataArray -SqlSettings $SqlConfig

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param(
        [Array] $Object,
        [System.Collections.IDictionary] $SqlSettings
    )
    if ($SqlSettings.SqlTableTranspose) {
        $Object = Format-TransposeTable -Object $Object
    }
    $SqlTable = Get-SqlQueryColumnInformation -SqlServer $SqlSettings.SqlServer -SqlDatabase $SqlSettings.SqlDatabase -Table $SqlSettings.SqlTable
    $PropertiesFromAllObject = Get-ObjectPropertiesAdvanced -Object $Object -AddProperties 'AddedWhen', 'AddedWho'
    $PropertiesFromTable = $SqlTable.Column_name

    if ($null -eq $SqlTable) {
        if ($SqlSettings.SqlTableCreate) {
            Write-Verbose "Send-SqlInsert - SqlTable doesn't exists, table creation is allowed, mapping will be done either on properties from object or from TableMapping defined in config"
            $TableMapping = New-SqlTableMapping -SqlTableMapping $SqlSettings.SqlTableMapping -Object $Object -Properties $PropertiesFromAllObject
            $CreateTableSQL = New-SqlQueryCreateTable -SqlSettings $SqlSettings -TableMapping $TableMapping
        } else {
            Write-Verbose "Send-SqlInsert - SqlTable doesn't exists, no table creation is allowed. Terminating"
            return "Error occured: SQL Table doesn't exists. SqlTableCreate option is disabled"
        }
    } else {
        if ($SqlSettings.SqlTableAlterIfNeeded) {
            if ( $SqlSettings.SqlTableMapping) {
                Write-Verbose "Send-SqlInsert - Sql Table exists, Alter is allowed, but SqlTableMapping is already defined"
                $TableMapping = New-SqlTableMapping -SqlTableMapping $SqlSettings.SqlTableMapping -Object $Object -Properties $PropertiesFromAllObject
            } else {
                Write-Verbose "Send-SqlInsert - Sql Table exists, Alter is allowed, and SqlTableMapping is not defined"
                $TableMapping = New-SqlTableMapping -SqlTableMapping $SqlSettings.SqlTableMapping -Object $Object -Properties $PropertiesFromAllObject
                $AlterTableSQL = New-SqlQueryAlterTable -SqlSettings $SqlSettings -TableMapping $TableMapping -ExistingColumns $SqlTable.Column_name
            }
        } else {
            if ( $SqlSettings.SqlTableMapping) {
                Write-Verbose "Send-SqlInsert - Sql Table exists, Alter is not allowed, SqlTableMaping is already defined"
                $TableMapping = New-SqlTableMapping -SqlTableMapping $SqlSettings.SqlTableMapping -Object $Object -Properties $PropertiesFromAllObject
            } else {
                Write-Verbose "Send-SqlInsert - Sql Table exists, Alter is not allowed, SqlTableMaping is not defined, using SqlTable Columns"
                $TableMapping = New-SqlTableMapping -SqlTableMapping $SqlSettings.SqlTableMapping -Object $Object -Properties $PropertiesFromTable -BasedOnSqlTable
            }
        }
    }
    $Queries = @(
        if ($CreateTableSQL) {
            foreach ($Sql in $CreateTableSQL) {
                $Sql
            }
        }
        if ($AlterTableSQL) {
            foreach ($Sql in $AlterTableSQL) {
                $Sql
            }
        }
        $SqlQueries = New-SqlQuery -Object $Object -SqlSettings $SqlSettings -TableMapping $TableMapping
        foreach ($Sql in $SqlQueries) {
            $Sql
        }
    )

    $ReturnData = foreach ($Query in $Queries) {
        try {
            if ($Query) {
                $Query # return query to log
                Invoke-DbaQuery -SqlInstance "$($SqlSettings.SqlServer)" -Database "$($SqlSettings.SqlDatabase)" -Query $Query -ErrorAction Stop # return output
            }
        } catch {
            $ErrorMessage = $_.Exception.Message -replace "`n", " " -replace "`r", " "
            "Error occured (Send-SqlInsert): $ErrorMessage" # return data
        }
    }
    return $ReturnData
}
