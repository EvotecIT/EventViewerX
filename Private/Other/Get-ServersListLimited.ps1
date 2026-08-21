function Get-ServersListLimited {
    [CmdletBinding()]
    param(
        [System.Collections.IDictionary] $Target,
        [System.Collections.IDictionary] $Definitions,
        [int] $EventID,
        [int64] $RecordID,
        [string] $LogName,
        [System.Management.Automation.Credential()] $Credential = [System.Management.Automation.PSCredential]::Empty,
        [switch] $Quiet,
        [string] $Who,
        [string] $Whom,
        [string] $NotWho,
        [string] $NotWhom
    )

    [Array] $DefaultLogNames = @(
        if (-not [String]::IsNullOrWhiteSpace($LogName)) {
            $LogName
        } elseif ($Definitions) {
            foreach ($Report in $Definitions.Keys) {
                if ($Report -in 'Enabled', 'SqlExport' -or -not $Definitions[$Report].Enabled) {
                    continue
                }
                foreach ($SubReport in $Definitions[$Report].Keys) {
                    if ($SubReport -in 'Enabled', 'SqlExport' -or -not $Definitions[$Report][$SubReport].Enabled) {
                        continue
                    }
                    if ($EventID -eq 0 -or $Definitions[$Report][$SubReport].Events -contains $EventID) {
                        $Definitions[$Report][$SubReport].LogName
                    }
                }
            }
        }
    ) | Sort-Object -Unique

    $NamedDataFilter = @{ }
    if ($Who -ne '') {
        $NamedDataFilter.SubjectUserName = $Who
    }
    if ($Whom -ne '') {
        $NamedDataFilter.TargetUserName = $Whom
    }
    $NamedDataExcludeFilter = @{ }
    if ($NotWho -ne '') {
        $NamedDataExcludeFilter.SubjectUserName = $NotWho
    }
    if ($NotWhom -ne '') {
        $NamedDataExcludeFilter.TargetUserName = $NotWhom
    }

    [Array] $ComputerTargets = @(
        if ($Target.Servers.Enabled) {
            if (-not $Quiet) {
                $Logger.AddInfoRecord('Preparing servers list - defined list')
            }
            foreach ($ServerKey in $Target.Servers.Keys) {
                if ($ServerKey -eq 'Enabled') {
                    continue
                }
                $ServerValue = $Target.Servers[$ServerKey]
                if ($ServerValue -is [System.Collections.IDictionary]) {
                    [Array] $ServerLogNames = if ($ServerValue.LogName) {
                        @($ServerValue.LogName)
                    } else {
                        $DefaultLogNames
                    }
                    foreach ($ComputerName in @($ServerValue.ComputerName)) {
                        foreach ($ServerLogName in $ServerLogNames) {
                            [PSCustomObject] @{
                                Server  = $ComputerName
                                LogName = $ServerLogName
                                Type    = 'Computer'
                            }
                        }
                    }
                } elseif ($ServerValue -is [Array] -or $ServerValue -is [String]) {
                    foreach ($ComputerName in @($ServerValue)) {
                        foreach ($ServerLogName in $DefaultLogNames) {
                            [PSCustomObject] @{
                                Server  = $ComputerName
                                LogName = $ServerLogName
                                Type    = 'Computer'
                            }
                        }
                    }
                }
            }
        }
        if ($Target.DomainControllers.Enabled) {
            if (-not $Quiet) {
                $Logger.AddInfoRecord('Preparing servers list - domain controllers autodetection')
            }
            [Array] $DomainControllers = if ($null -ne $Credential -and $Credential -ne [System.Management.Automation.PSCredential]::Empty) {
                (Get-WinADDomainControllers -SkipEmpty -Credential $Credential).HostName
            } else {
                (Get-WinADDomainControllers -SkipEmpty).HostName
            }
            foreach ($ComputerName in $DomainControllers) {
                foreach ($ServerLogName in $DefaultLogNames) {
                    [PSCustomObject] @{
                        Server  = $ComputerName
                        LogName = $ServerLogName
                        Type    = 'Computer'
                    }
                }
            }
        }
    )

    [Array] $FileTargets = @(
        if ($Target.LocalFiles.Enabled) {
            if (-not $Quiet) {
                $Logger.AddInfoRecord('Preparing file list - defined event log files')
            }
            foreach ($File in @(Get-EventLogFileList -Sections $Target.LocalFiles)) {
                foreach ($FileLogName in $DefaultLogNames) {
                    [PSCustomObject] @{
                        Server  = $File
                        LogName = $FileLogName
                        Type    = 'File'
                    }
                }
            }
        }
    )

    [Array] $ExtendedInput = foreach ($Server in @($ComputerTargets) + @($FileTargets)) {
        [PSCustomObject] @{
            Server                 = $Server.Server
            LogName                = $Server.LogName
            Type                   = $Server.Type
            EventID                = $EventID
            RecordID               = $RecordID
            NamedDataFilter        = if ($NamedDataFilter.Count -ne 0) { $NamedDataFilter } else { }
            NamedDataExcludeFilter = if ($NamedDataExcludeFilter.Count -ne 0) { $NamedDataExcludeFilter } else { }
        }
    }
    if ($ExtendedInput.Count -gt 1) {
        $ExtendedInput
    } else {
        , $ExtendedInput
    }
}
