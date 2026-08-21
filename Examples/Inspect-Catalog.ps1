Import-Module PSEventViewer -Force

Get-EVXLog -LogName 'Microsoft-Windows-PowerShell/*' -Force |
    Select-Object LogName, IsEnabled, LogMode, MaximumSizeInBytes

Get-EVXProvider `
    -Name Microsoft-Windows-PowerShell `
    -IncludeEvents |
    Select-Object Name, Id, LogLinks, Levels, Tasks, Opcodes, Keywords, Events

Get-EVXProvider -Name 'Microsoft-Windows-Kernel-*' -NameOnly
