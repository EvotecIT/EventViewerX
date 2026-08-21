function New-SqlTableMapping {
    <#
    .SYNOPSIS
    Creates a new SQL table mapping based on the provided parameters.

    .DESCRIPTION
    This function creates a new SQL table mapping based on the provided parameters. It generates a mapping for each property in the object based on its data type.

    .PARAMETER SqlTableMapping
    The existing SQL table mapping to update.

    .PARAMETER Object
    The object for which the SQL table mapping is being created.

    .PARAMETER Properties
    The properties of the object for which the SQL table mapping is being created.

    .PARAMETER BasedOnSqlTable
    Indicates whether the mapping should be based on an existing SQL table.

    .EXAMPLE
    $sqlTableMapping = New-SqlTableMapping -Object $myObject -Properties $myProperties

    Creates a new SQL table mapping for the object $myObject using the properties $myProperties.

    .EXAMPLE
    $sqlTableMapping = New-SqlTableMapping -SqlTableMapping $existingMapping -Object $myObject -Properties $myProperties -BasedOnSqlTable

    Updates the existing SQL table mapping $existingMapping based on the object $myObject and its properties $myProperties.

    #>
    [CmdletBinding()]
    param(
        [Object] $SqlTableMapping,
        [Object] $Object,
        $Properties,
        [switch] $BasedOnSqlTable
    )
    if ($SqlTableMapping) {
        $TableMapping = $SqlTableMapping
    } else {
        $TableMapping = @{}
        if ($BasedOnSqlTable) {
            foreach ($Property in $Properties) {
                $FieldName = $Property
                $FieldNameSql = $Property
                $TableMapping.$FieldName = $FieldNameSQL
            }
        } else {
            # Gets the highest object
            foreach ($O in $Properties.HighestObject) {
                # goes thru properties (but not properties of highest object but for all properties of all objects
                # if there is value it will use the ones from highest object if not it will utilize nvarchar

                foreach ($Property in $Properties.Properties) {

                    #foreach ($E in $O.PSObject.Properties) {
                    $FieldName = $Property
                    $FieldValue = $O.$Property

                    $FieldNameSQL = $FieldName.Replace(' ', '') #.Replace('-', '')

                    if ($FieldValue -is [DateTime]) {
                        $TableMapping.$FieldName = "$FieldNameSQL,[datetime],null"
                        #Write-Verbose "New-SqlTableMapping - FieldName: $FieldName FieldValue: $FieldValue FieldNameSQL: $FieldNameSQL FieldDataType: DateTime"
                    } elseif ($FieldValue -is [int] -or $FieldValue -is [Int64]) {
                        $TableMapping.$FieldName = "$FieldNameSQL,[bigint]"
                        # Write-Verbose "New-SqlTableMapping - FieldName: $FieldName FieldValue: $FieldValue FieldNameSQL: $FieldNameSQL FieldDataType: BigInt"
                    } elseif ($FieldValue -is [bool]) {
                        $TableMapping.$FieldName = "$FieldNameSQL,[bit]"
                        #Write-Verbose "New-SqlTableMapping - FieldName: $FieldName FieldValue: $FieldValue FieldNameSQL: $FieldNameSQL FieldDataType: Bit/Bool"
                    } else {
                        $TableMapping.$FieldName = "$FieldNameSQL"
                        #Write-Verbose "New-SqlTableMapping - FieldName: $FieldName FieldValue: $FieldValue FieldNameSQL: $FieldNameSQL FieldDataType: NvarChar"
                    }
                }
            }
        }
    }
    #Write-Verbose 'New-SqlTableMapping - Ending'
    return $TableMapping
}
