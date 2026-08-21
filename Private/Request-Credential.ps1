function Request-Credentials {
    <#
    .SYNOPSIS
    Requests credentials for authentication purposes.

    .DESCRIPTION
    The Request-Credentials function is used to prompt the user for credentials. It provides options to input the username and password directly, read the password from a file, convert the password to a secure string, and handle various error scenarios.

    .PARAMETER UserName
    Specifies the username for authentication.

    .PARAMETER Password
    Specifies the password for authentication.

    .PARAMETER AsSecure
    Indicates whether the password should be converted to a secure string.

    .PARAMETER FromFile
    Specifies whether the password should be read from a file.

    .PARAMETER Output
    Indicates whether the function should return output in case of errors.

    .PARAMETER NetworkCredentials
    Specifies if network credentials are being requested.

    .PARAMETER Service
    Specifies the service for which credentials are being requested.

    .EXAMPLE
    Request-Credentials -UserName 'JohnDoe' -Password 'P@ssw0rd' -AsSecure
    Requests credentials for the user 'JohnDoe' with the password 'P@ssw0rd' in a secure format.

    .EXAMPLE
    Request-Credentials -FromFile -Password 'C:\Credentials.txt' -Output -Service 'FTP'
    Reads the password from the file 'C:\Credentials.txt' and returns an error message if the file is unreadable for the FTP service.

    #>
    [CmdletBinding()]
    param(
        [string] $UserName,
        [string] $Password,
        [switch] $AsSecure,
        [switch] $FromFile,
        [switch] $Output,
        [switch] $NetworkCredentials,
        [string] $Service
    )
    if ($FromFile) {
        if (($Password -ne '') -and (Test-Path $Password)) {
            # File is there and we are reading it into Password
            Write-Verbose "Request-Credentials - Reading password from file $Password"
            $Password = Get-Content -Path $Password
        } else {
            # File is not there or couldn't be read
            if ($Output) {
                return @{ Status = $false; Output = $Service; Extended = 'File with password unreadable.' }
            } else {
                Write-Warning "Request-Credentials - Secure password from file couldn't be read. File not readable. Terminating."
                return
            }
        }
    }
    if ($AsSecure) {
        try {
            $NewPassword = $Password | ConvertTo-SecureString -ErrorAction Stop
        } catch {
            $ErrorMessage = $_.Exception.Message -replace "`n", " " -replace "`r", " "
            if ($ErrorMessage -like '*Key not valid for use in specified state*') {
                if ($Output) {
                    return @{ Status = $false; Output = $Service; Extended = "Couldn't use credentials provided. Most likely using credentials from other user/session/computer." }
                } else {
                    Write-Warning -Message "Request-Credentials - Couldn't use credentials provided. Most likely using credentials from other user/session/computer."
                    return
                }
            } else {
                if ($Output) {
                    return @{ Status = $false; Output = $Service; Extended = $ErrorMessage }
                } else {
                    Write-Warning -Message "Request-Credentials - $ErrorMessage"
                    return
                }
            }
        }

    } else {
        $NewPassword = $Password
    }
    if ($UserName -and $NewPassword) {
        if ($AsSecure) {
            $Credentials = New-Object System.Management.Automation.PSCredential($Username, $NewPassword)
        } else {
            Try {
                $SecurePassword = $Password | ConvertTo-SecureString -asPlainText -Force -ErrorAction Stop
            } catch {
                $ErrorMessage = $_.Exception.Message -replace "`n", " " -replace "`r", " "
                if ($ErrorMessage -like '*Key not valid for use in specified state*') {
                    if ($Output) {
                        return  @{ Status = $false; Output = $Service; Extended = "Couldn't use credentials provided. Most likely using credentials from other user/session/computer." }
                    } else {
                        Write-Warning -Message "Request-Credentials - Couldn't use credentials provided. Most likely using credentials from other user/session/computer."
                        return
                    }
                } else {
                    if ($Output) {
                        return @{ Status = $false; Output = $Service; Extended = $ErrorMessage }
                    } else {
                        Write-Warning -Message "Request-Credentials - $ErrorMessage"
                        return
                    }
                }
            }
            $Credentials = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)
        }
    } else {
        if ($Output) {
            return @{ Status = $false; Output = $Service; Extended = 'Username or/and Password is empty' }
        } else {
            Write-Warning -Message 'Request-Credentials - UserName or Password are empty.'
            return
        }
    }
    if ($NetworkCredentials) {
        return $Credentials.GetNetworkCredential()
    } else {
        return $Credentials
    }
}
