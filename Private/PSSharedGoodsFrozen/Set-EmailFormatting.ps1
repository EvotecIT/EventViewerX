function Set-EmailFormatting {
    <#
    .SYNOPSIS
    Sets the formatting for an email template.

    .DESCRIPTION
    This function sets the formatting for an email template by applying specified styles and parameters.

    .PARAMETER Template
    The email template to be formatted.

    .PARAMETER FormattingParameters
    A dictionary containing the styles to be applied to the template.

    .PARAMETER ConfigurationParameters
    A dictionary containing configuration parameters for formatting.

    .PARAMETER Logger
    An object used for logging information.

    .PARAMETER SkipNewLines
    Switch to skip adding new lines to the template.

    .PARAMETER AddAfterOpening
    An array of strings to add after the opening of the template.

    .PARAMETER AddBeforeClosing
    An array of strings to add before the closing of the template.

    .PARAMETER Image
    An optional image to be included in the template.

    .EXAMPLE
    Set-EmailFormatting -Template "Hello, <<Name>>!" -FormattingParameters @{ Styles = @{ Name = "b" } } -ConfigurationParameters @{ DisplayConsole = $true } -Logger $logger -Image "logo.png"

    Description:
    Sets the formatting for an email template with a bold style applied to the name placeholder and includes a logo image.

    #>
    [CmdletBinding()]
    param (
        $Template,
        [System.Collections.IDictionary] $FormattingParameters,
        [System.Collections.IDictionary] $ConfigurationParameters,
        [PSCustomObject] $Logger,
        [switch] $SkipNewLines,
        [string[]] $AddAfterOpening,
        [string[]] $AddBeforeClosing,
        [string] $Image
    )
    if ($ConfigurationParameters) {
        $WriteParameters = $ConfigurationParameters.DisplayConsole
    } else {
        $WriteParameters = @{ ShowTime = $true; LogFile = ""; TimeFormat = "yyyy-MM-dd HH:mm:ss" }
    }

    if ($Image) {
        $Template = $Template -replace '<<Image>>', $Image
    }


    $Body = "<body>"
    if ($AddAfterOpening) {
        $Body += $AddAfterOpening
    }

    if (-not $SkipNewLines) {
        $Template = $Template.Split("`n") # https://blogs.msdn.microsoft.com/timid/2014/07/09/one-liner-fun-with-multi-line-blocktext-and-split-split/
        if ($Logger) {
            $Logger.AddInfoRecord("Preparing template - adding HTML <BR> tags...")
        } else {
            Write-Color @WriteParameters -Text "[i] Preparing template ", "adding", " HTML ", "<BR>", " tags." -Color White, Yellow, White, Yellow
        }
        foreach ($t in $Template) {
            $Body += "$t<br>"
        }
    } else {
        $Body += $Template
    }
    foreach ($style in $FormattingParameters.Styles.GetEnumerator()) {
        foreach ($value in $style.Value) {
            if ($value -eq "") { continue }
            if ($Logger) {
                $Logger.AddInfoRecord("Preparing template - adding HTML $($style.Name) tag for $value.")
            } else {
                Write-Color @WriteParameters -Text "[i] Preparing template ", "adding", " HTML ", "$($style.Name)", " tag for ", "$value", ' tags...' -Color White, Yellow, White, Yellow, White, Yellow
            }
            $Body = $Body.Replace($value, "<$($style.Name)>$value</$($style.Name)>")
        }
    }

    foreach ($color in $FormattingParameters.Colors.GetEnumerator()) {
        foreach ($value in $color.Value) {
            if ($value -eq "") { continue }
            if ($Logger) {
                $Logger.AddInfoRecord("Preparing template - adding HTML $($color.Name) tag for $value.")
            } else {
                Write-Color @WriteParameters -Text "[i] Preparing template ", "adding", " HTML ", "$($color.Name)", " tag for ", "$value", ' tags...' -Color White, Yellow, White, Yellow, White, Yellow
            }
            $Body = $Body.Replace($value, "<span style=color:$($color.Name)>$value</span>")
        }
    }
    foreach ($links in $FormattingParameters.Links.GetEnumerator()) {
        foreach ($link in $links.Value) {
            if ($link.Link -like "*@*") {
                if ($Logger) {
                    $Logger.AddInfoRecord("Preparing template - adding EMAIL Links for $($links.Key).")
                } else {
                    Write-Color @WriteParameters -Text "[i] Preparing template ", "adding", " EMAIL ", "Links for", " $($links.Key)..." -Color White, Yellow, White, White, Yellow, White
                }
                $Body = $Body -replace "<<$($links.Key)>>", "<span style=color:$($link.Color)><a href='mailto:$($link.Link)?subject=$($Link.Subject)'>$($Link.Text)</a></span>"
            } else {
                if ($Logger) {
                    $Logger.AddInfoRecord("[i] Preparing template - adding HTML Links for $($links.Key)")
                } else {
                    Write-Color @WriteParameters -Text "[i] Preparing template ", "adding", " HTML ", "Links for", " $($links.Key)..." -Color White, Yellow, White, White, Yellow, White
                }
                $Body = $Body -replace "<<$($links.Key)>>", "<span style=color:$($link.Color)><a href='$($link.Link)'>$($Link.Text)</a></span>"
            }
        }
    }
    if ($AddAfterOpening) {
        $Body += $AddBeforeClosing
    }
    $Body += '</body>'
    if ($ConfigurationParameters) {
        if ($ConfigurationParameters.DisplayTemplateHTML -eq $true) { Get-HTML($Body) }
    }
    return $Body
}
