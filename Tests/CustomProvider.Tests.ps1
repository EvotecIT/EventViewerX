Describe 'Custom manifest provider lifecycle' {
    BeforeDiscovery {
        $Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $Principal = [Security.Principal.WindowsPrincipal]::new($Identity)
        $IsElevated = $Principal.IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator
        )
    }

    It 'exports the provider package workflow' {
        $Commands = @(
            'Test-EVXProviderDefinition'
            'New-EVXProviderPackage'
            'Get-EVXProvider'
            'Install-EVXProviderPackage'
            'Uninstall-EVXProviderPackage'
        )
        foreach ($Name in $Commands) {
            (Get-Command $Name).CommandType | Should -Be 'Cmdlet'
        }

        $WriteCommand = Get-Command Write-EVXEvent
        $WriteCommand.Parameters.Keys | Should -Contain 'EventName'
        $WriteCommand.Parameters.Keys | Should -Contain 'Data'
    }

    It 'validates one friendly event hashtable without a conversion cmdlet' {
        $Definition = @{
            ProviderName = 'Contoso.Pester'
            ProviderGuid = '0c47facd-a6c5-45bf-9e0d-035274510a28'
            Version      = '1.0.0'
            Events       = @{
                Name    = 'ScanCompleted'
                Id      = 1000
                Message = 'Scan of {ComputerName} found {FindingCount} issues.'
                Fields  = [ordered] @{
                    ComputerName = 'String'
                    FindingCount = 'UInt32'
                }
            }
        }

        (Test-EVXProviderDefinition $Definition).IsValid | Should -BeTrue
    }

    It 'rejects misspelled hashtable properties instead of ignoring them' {
        {
            @{
                ProviderName = 'Contoso.Typo'
                ProviderGuid = [guid]::NewGuid()
                Version      = '1.0.0'
                Events       = @{
                    Name = 'Example'
                    Id   = 1000
                }
                Evnts        = @{}
            } | Test-EVXProviderDefinition -ErrorAction Stop
        } | Should -Throw '*Evnts*'
    }

    It 'returns structured validation for explicit null JSON members' {
        $DefinitionPath = Join-Path $TestDrive 'null-provider.json'
        [System.IO.File]::WriteAllText(
            $DefinitionPath,
            '{"name":null,"id":"0c47facd-a6c5-45bf-9e0d-035274510a28","packageVersion":"1.0.0"}'
        )

        $Result = Test-EVXProviderDefinition `
            -Path $DefinitionPath `
            -ErrorAction Stop

        $Result.IsValid | Should -BeFalse
        $Result.Errors.Code | Should -Contain 'ProviderNameRequired'
    }

    It 'builds, installs, writes, reads, and removes a provider package' -Skip:(-not $IsElevated) {
        $Suffix = [guid]::NewGuid().ToString('N')
        $ProviderName = "Evotec-EventViewerX-Pester-$Suffix"
        $ProviderGuid = [guid]::NewGuid()
        $PackagePath = Join-Path $TestDrive "$Suffix.evxprovider"
        $Installed = $false
        try {
            $Definition = @{
                ProviderName = $ProviderName
                ProviderGuid = $ProviderGuid
                Version      = '1.0.0'
                Events       = @{
                    Name    = 'ScanCompleted'
                    Id      = 1000
                    Message = 'Scan of {ComputerName} found {FindingCount} issues.'
                    Fields  = [ordered] @{
                        ComputerName = 'String'
                        FindingCount = 'UInt32'
                    }
                }
            }
            $Package = New-EVXProviderPackage `
                -Definition $Definition `
                -OutputPath $PackagePath `
                -Confirm:$false `
                -ErrorAction Stop
            $Package.OutputPath | Should -Be $PackagePath

            $Install = Install-EVXProviderPackage `
                -Path $PackagePath `
                -Confirm:$false `
                -ErrorAction Stop
            $Installed = $true
            $Install.Status.ToString() | Should -Be 'Installed'

            $ProviderRoot = Split-Path `
                -Path $Install.InstallPath `
                -Parent
            $Icacls = Join-Path `
                $env:SystemRoot `
                'System32\icacls.exe'
            & $Icacls `
                $ProviderRoot `
                /grant `
                '*S-1-1-0:(OI)(CI)(F)' |
                Out-Null
            $LASTEXITCODE | Should -Be 0
            $AclRepair = Install-EVXProviderPackage `
                -Path $PackagePath `
                -Confirm:$false `
                -ErrorAction Stop
            $AclRepair.Status.ToString() |
                Should -Be 'Unchanged'
            $ProviderAcl = Get-Acl `
                -LiteralPath $ProviderRoot
            $AclSids = @(
                $ProviderAcl.Access.IdentityReference |
                    ForEach-Object {
                    $_.Translate(
                        [Security.Principal.SecurityIdentifier]
                    ).Value
                }
            )
            $AclSids | Should -Not -Contain 'S-1-1-0'

            $Marker = 'EVX-%1-100%-' + [guid]::NewGuid().ToString('N')
            $StartTime = (Get-Date).AddSeconds(-1)
            $Write = Write-EVXEvent `
                -ProviderName $ProviderName `
                -EventName ScanCompleted `
                -Data @{
                    FindingCount = 7
                    ComputerName = $Marker
                } `
                -Confirm:$false `
                -ErrorAction Stop
            $Write.Success | Should -BeTrue

            $WinEvent = $null
            $EVXEvent = $null
            $EVXFilter = New-EVXFilter `
                -ProviderName $ProviderName `
                -EventId 1000 `
                -StartTime $StartTime
            foreach ($Attempt in 1..20) {
                $WinEvent = Get-WinEvent `
                    -FilterHashtable @{
                        LogName      = "$ProviderName/Operational"
                        ProviderName = $ProviderName
                        Id           = 1000
                        StartTime    = $StartTime
                    } `
                    -MaxEvents 20 `
                    -ErrorAction SilentlyContinue |
                    Where-Object { $_.Properties.Value -contains $Marker } |
                    Select-Object -First 1
                $EVXEvent = Get-EVXEvent `
                    -LogName "$ProviderName/Operational" `
                    -Filter $EVXFilter `
                    -ReadMode Full `
                    -MaxEvents 20 `
                    -ErrorAction SilentlyContinue |
                    Where-Object { $_.Data.Values -contains $Marker } |
                    Select-Object -First 1
                if ($WinEvent -and $EVXEvent) {
                    break
                }
                Start-Sleep -Milliseconds 200
            }

            $WinEvent | Should -Not -BeNullOrEmpty
            $EVXEvent | Should -Not -BeNullOrEmpty
            $EVXEvent.Message | Should -Be $WinEvent.Message
            $EVXEvent.Message |
                Should -Match ([regex]::Escape($Marker))
            $EVXEvent.Data.Values |
                Should -Be $WinEvent.Properties.Value
            (
                Get-EVXProvider -InstalledPackage |
                    Where-Object {
                        $_.ProviderName -eq $ProviderName -and
                        $_.IsRegistered
                    }
            ).PackageVersion | Should -Be '1.0.0'

            $ReplacementPath = Join-Path `
                $TestDrive `
                "$Suffix-replacement.evxprovider"
            $ReplacementDefinition = @{
                ProviderName = $ProviderName
                ProviderGuid = $ProviderGuid
                Version      = '1.0.0'
                Events       = @{
                    Name    = 'ScanCompleted'
                    Id      = 1000
                    Message = 'Completed scan of {ComputerName}; findings: {FindingCount}.'
                    Fields  = [ordered] @{
                        ComputerName = 'String'
                        FindingCount = 'UInt32'
                    }
                }
            }
            New-EVXProviderPackage `
                -Definition $ReplacementDefinition `
                -OutputPath $ReplacementPath `
                -Confirm:$false `
                -ErrorAction Stop |
                Out-Null
            {
                Install-EVXProviderPackage `
                    -Path $ReplacementPath `
                    -Confirm:$false `
                    -ErrorAction Stop
            } | Should -Throw '*same-version replacement*'

            $Replacement = Install-EVXProviderPackage `
                -Path $ReplacementPath `
                -AllowSameVersionReplacement `
                -Confirm:$false `
                -ErrorAction Stop
            $Replacement.Status.ToString() |
                Should -Be 'Upgraded'
            $Replacement.InstallPath |
                Should -Not -Be $Install.InstallPath
            $Inventory = @(
                Get-EVXProvider -InstalledPackage |
                    Where-Object ProviderName -EQ $ProviderName
            )
            @($Inventory | Where-Object IsActive).Count |
                Should -Be 1
            $Inventory.Count | Should -BeGreaterOrEqual 2

            Copy-Item `
                -LiteralPath $PackagePath `
                -Destination (
                    Join-Path `
                        $Replacement.InstallPath `
                        'provider.evxprovider'
                ) `
                -Force
            $ArchiveRepair = Install-EVXProviderPackage `
                -Path $ReplacementPath `
                -Confirm:$false `
                -ErrorAction Stop
            $ArchiveRepair.Status.ToString() |
                Should -Be 'Repaired'
            $ArchiveRepair.InstallPath |
                Should -Not -Be $Replacement.InstallPath

            '{}' | Set-Content `
                -LiteralPath (
                    Join-Path `
                        $ArchiveRepair.InstallPath `
                        'provider.definition.json'
                ) `
                -Encoding UTF8
            $TamperRepair = Install-EVXProviderPackage `
                -Path $ReplacementPath `
                -Confirm:$false `
                -ErrorAction Stop
            $TamperRepair.Status.ToString() |
                Should -Be 'Repaired'
            $TamperRepair.InstallPath |
                Should -Not -Be $ArchiveRepair.InstallPath

            $WevtUtil = Join-Path `
                $env:SystemRoot `
                'System32\wevtutil.exe'
            & $WevtUtil `
                um `
                (Join-Path $TamperRepair.InstallPath 'provider.man')
            $LASTEXITCODE | Should -Be 0
            $Inactive = Get-EVXProvider -InstalledPackage |
                Where-Object {
                    $_.ProviderName -eq $ProviderName -and
                    $_.IsActive
                }
            $Inactive.IsRegistered | Should -BeFalse

            $RegistrationRepair = Install-EVXProviderPackage `
                -Path $ReplacementPath `
                -Confirm:$false `
                -ErrorAction Stop
            $RegistrationRepair.Status.ToString() |
                Should -Be 'Repaired'
            $RegistrationRepair.InstallPath |
                Should -Not -Be $TamperRepair.InstallPath
            (
                Get-EVXProvider -InstalledPackage |
                    Where-Object {
                        $_.ProviderName -eq $ProviderName -and
                        $_.IsActive
                    }
            ).IsRegistered | Should -BeTrue
        } catch {
            if ($_.Exception.ToString() -match 'code 6801|Transaction support within the specified resource manager') {
                Set-ItResult -Skipped -Because 'The Windows Transactional Registry resource manager is unavailable on this host.'
            } else {
                throw
            }
        } finally {
            if ($Installed) {
                Uninstall-EVXProviderPackage `
                    -ProviderName $ProviderName `
                    -RemoveFiles `
                    -Confirm:$false `
                    -ErrorAction SilentlyContinue |
                    Out-Null
            }
        }
    }
}
