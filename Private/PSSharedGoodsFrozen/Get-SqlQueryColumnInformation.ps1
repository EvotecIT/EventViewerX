function Get-SqlQueryColumnInformation {
    <#
    .SYNOPSIS
    Retrieves column information for a specified table in a SQL database.

    .DESCRIPTION
    This function retrieves column information for a specified table in a SQL database using the INFORMATION_SCHEMA.COLUMNS view.

    .PARAMETER SqlServer
    The SQL Server instance where the database is located.

    .PARAMETER SqlDatabase
    The name of the SQL database.

    .PARAMETER Table
    The name of the table for which column information is to be retrieved.

    .EXAMPLE
    Get-SqlQueryColumnInformation -SqlServer "localhost" -SqlDatabase "MyDatabase" -Table "MyTable"
    Retrieves column information for the table "MyTable" in the database "MyDatabase" on the SQL Server instance "localhost".

    #>
    [CmdletBinding()]
    param (
        [string] $SqlServer,
        [string] $SqlDatabase,
        [string] $Table
    )
    $Table = $Table.Replace("dbo.", '').Replace('[', '').Replace(']', '') # removes dbo and [] from dbo.[Table] as INFORMATION_SCHEMA expects it without
    $SqlDatabase = $SqlDatabase.Replace('[', '').Replace(']', '') # makes sure we know what we have
    $SqlDatabase = "[$SqlDatabase]"
    $Query = "SELECT * FROM $SqlDatabase.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$Table'"
    $SqlReturn = @(
        try {
            Invoke-DbaQuery -ErrorAction Stop -SqlInstance $SqlServer -Query $Query #-Verbose
        } catch {
            $ErrorMessage = $_.Exception.Message -replace "`n", " " -replace "`r", " "
            "Error occured (Get-SqlQueryColumnInformation): $ErrorMessage" # return of error
        }
    )
    return $SQLReturn
}
