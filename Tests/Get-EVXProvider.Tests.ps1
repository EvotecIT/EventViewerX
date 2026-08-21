Describe 'Get-EVXProvider' {
    It 'returns detached provider metadata by default' {
        $providers = @(Get-EVXProvider -Name 'Microsoft-Windows-Kernel-*' | Select-Object -First 3)
        $providers | Should -Not -BeNullOrEmpty
        $providers[0].Name | Should -Match '^Microsoft-Windows-Kernel-'
        $providers[0].LogLinks | Should -Not -BeNull
    }

    It 'supports the low-cost provider-name projection' {
        $providers = @(Get-EVXProvider -Name 'Microsoft-Windows-Kernel-*' -NameOnly)
        $providers | Should -Not -BeNullOrEmpty
        $providers[0] | Should -BeOfType [string]
    }
}
