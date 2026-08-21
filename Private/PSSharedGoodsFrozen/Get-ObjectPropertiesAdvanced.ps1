function Get-ObjectPropertiesAdvanced {
    <#
    .SYNOPSIS
    Retrieves properties of objects and provides the ability to add custom properties.

    .DESCRIPTION
    This function retrieves properties of objects and allows users to add custom properties to the output. It calculates the highest count of properties among the objects and returns the properties in an array.

    .PARAMETER Object
    Specifies the object or objects whose properties need to be retrieved.

    .PARAMETER AddProperties
    Specifies an array of custom properties to be added to the output.

    .PARAMETER Sort
    Indicates whether the properties should be sorted alphabetically.

    .EXAMPLE
    $objects = Get-ObjectPropertiesAdvanced -Object $myObject -AddProperties @("CustomProperty1", "CustomProperty2") -Sort
    This example retrieves properties of $myObject and adds custom properties "CustomProperty1" and "CustomProperty2" to the output, sorted alphabetically.

    #>
    [CmdletBinding()]
    param (
        [object] $Object,
        [string[]] $AddProperties, # provides ability to add some custom properties
        [switch] $Sort
    )
    $Data = @{ }
    $Properties = New-ArrayList
    $HighestCount = 0

    foreach ($O in $Object) {
        $ObjectProperties = $O.PSObject.Properties.Name
        # $Test = $ObjectProperties -join ','
        $Count = $ObjectProperties.Count
        if ($Count -gt $HighestCount) {
            $Data.HighestCount = $Count
            $Data.HighestObject = $O
            $HighestCount = $Count
        }
        foreach ($Property in $ObjectProperties) {
            Add-ToArrayAdvanced -List $Properties -Element $Property -SkipNull -RequireUnique
        }
    }
    foreach ($Property in $AddProperties) {
        Add-ToArrayAdvanced -List $Properties -Element $Property -SkipNull -RequireUnique
    }
    $Data.Properties = if ($Sort) { $Properties | Sort-Object } else { $Properties }
    #Write-Verbose "Get-ObjectPropertiesAdvanced - HighestCount: $($Data.HighestCount)"
    #Write-Verbose "Get-ObjectPropertiesAdvanced - Properties: $($($Data.Properties) -join ',')"

    # returns for example
    # $Data.HighestCount = 100
    # $Data.HighestObject = $Object
    # $Data.Properties = array of strings
    return $Data
}
