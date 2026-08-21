function Set-EmailHead {
    <#
    .SYNOPSIS
    Sets the HTML head section for an email with specified formatting options.

    .DESCRIPTION
    The Set-EmailHead function generates the HTML head section for an email with customizable formatting options. It includes meta tags for content type, viewport settings, and description. Additionally, it defines styles for the body, tables, headings, lists, and more.

    .PARAMETER FormattingOptions
    Specifies the formatting options to be applied to the email content.

    .EXAMPLE
    $formatting = @{
        FontFamily = 'Arial';
        FontSize = '12px';
        FontTableDataFamily = 'Arial';
        FontTableDataSize = '10px';
        FontTableHeadingFamily = 'Arial';
        FontTableHeadingSize = '12px';
    }
    Set-EmailHead -FormattingOptions $formatting
    #>
    [cmdletBinding()]
    param(
        [System.Collections.IDictionary] $FormattingOptions
    )
    $head = @"
<!DOCTYPE html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=utf-8">
<meta content="width=device-width, initial-scale=1" name="viewport">
<meta name="description" content="Password Expiration Email">
    <style>
    BODY {
        background-color: white;
        font-family: $($FormattingOptions.FontFamily);
        font-size: $($FormattingOptions.FontSize);
    }

    TABLE {
        border-width: 1px;
        border-style: solid;
        border-color: black;
        border-collapse: collapse;
        font-family: $($FormattingOptions.FontTableDataFamily);
        font-size: $($FormattingOptions.FontTableDataSize);
    }

    TH {
        border-width: 1px;
        padding: 3px;
        border-style: solid;
        border-color: black;
        background-color: #00297A;
        color: white;
        font-family: $($FormattingOptions.FontTableHeadingFamily);
        font-size: $($FormattingOptions.FontTableHeadingSize);
    }
    TR {
        font-family: $($FormattingOptions.FontTableDataFamily);
        font-size: $($FormattingOptions.FontTableDataSize);
    }

    UL {
        font-family: $($FormattingOptions.FontFamily);
        font-size: $($FormattingOptions.FontSize);
    }

    LI {
        font-family: $($FormattingOptions.FontFamily);
        font-size: $($FormattingOptions.FontSize);
    }

    TD {
        border-width: 1px;
        padding-right: 2px;
        padding-left: 2px;
        padding-top: 0px;
        padding-bottom: 0px;
        border-style: solid;
        border-color: black;
        background-color: white;
        font-family: $($FormattingOptions.FontTableDataFamily);
        font-size: $($FormattingOptions.FontTableDataSize);
    }

    H2 {
        font-family: $($FormattingOptions.FontHeadingFamily);
        font-size: $($FormattingOptions.FontHeadingSize);
    }

    P {
        font-family: $($FormattingOptions.FontFamily);
        font-size: $($FormattingOptions.FontSize);
    }
</style>
</head>
"@
    return $Head
}
