function Set-EmailReportBranding {
    <#
    .SYNOPSIS
    Sets the branding for the email report.

    .DESCRIPTION
    This function sets the branding for the email report by customizing the company logo and link.

    .PARAMETER FormattingParameters
    Specifies the formatting options for the email report branding.

    .EXAMPLE
    $brandingParams = @{
        CompanyBranding = @{
            Link = "https://www.example.com"
            Inline = $true
            Logo = "C:\CompanyLogo.png"
            Width = "200px"
            Height = "100px"
        }
    }
    Set-EmailReportBranding -FormattingParameters $brandingParams
    #>
    [cmdletBinding()]
    param(
        [alias('FormattingOptions')] $FormattingParameters
    )
    if ($FormattingParameters.CompanyBranding.Link) {
        $Report = "<a style=`"text-decoration:none`" href=`"$($FormattingParameters.CompanyBranding.Link)`" class=`"clink logo-container`">"
    } else {
        $Report = ''
    }
    if ($FormattingParameters.CompanyBranding.Inline) {
        $Report += "<img width=<fix> height=<fix> src=`"cid:logo`" border=`"0`" class=`"company-logo`" alt=`"company-logo`"></a>"
    } else {
        $Report += "<img width=<fix> height=<fix> src=`"$($FormattingParameters.CompanyBranding.Logo)`" border=`"0`" class=`"company-logo`" alt=`"company-logo`"></a>"
    }
    if ($FormattingParameters.CompanyBranding.Width -ne "") {
        $Report = $Report -replace "width=<fix>", "width=$($FormattingParameters.CompanyBranding.Width)"
    } else {
        $Report = $Report -replace "width=<fix>", ""
    }
    if ($FormattingParameters.CompanyBranding.Height -ne "") {
        $Report = $Report -replace "height=<fix>", "height=$($FormattingParameters.CompanyBranding.Height)"
    } else {
        $Report = $Report -replace "height=<fix>", ""
    }
    return $Report
}
