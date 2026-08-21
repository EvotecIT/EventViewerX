Describe 'Get-EVXEvent - Expanded object property order' {
    It 'returns expanded data in alphabetical order' {
        $filePath = Join-Path -Path $PSScriptRoot -ChildPath 'Logs/Active Directory Web Services.evtx'
        $event = Get-EVXEvent -Path $filePath -MaxEvents 1 -ReadMode StructuredData -ExpandData -Verbose | Select-Object -First 1
        $expected = $event.Data.Keys | Sort-Object -Culture ([System.Globalization.CultureInfo]::InvariantCulture)
        $actual = $event.PSObject.Properties |
            Where-Object { $event.Data.ContainsKey($_.Name) } |
            ForEach-Object Name
        $actual | Should -Be $expected
    }
}
