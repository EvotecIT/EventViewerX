function Set-EmailWordReplacements($Body, $Replace, $ReplaceWith, [switch] $RegEx) {
    <#
    .SYNOPSIS
    Replaces words or patterns in an email body with specified replacements.

    .DESCRIPTION
    This function replaces words or patterns in the email body with specified replacements. It provides the option to use regular expressions for more complex replacements.

    .PARAMETER Body
    The email body where the word replacements will be applied.

    .PARAMETER Replace
    The word or pattern to be replaced in the email body.

    .PARAMETER ReplaceWith
    The replacement for the word or pattern.

    .PARAMETER RegEx
    Indicates whether to use regular expressions for replacements.

    .EXAMPLE
    $body = "Hello, my name is John."
    Set-EmailWordReplacements -Body $body -Replace 'John' -ReplaceWith 'Jane'
    # This will replace "John" with "Jane" in the email body.

    .EXAMPLE
    $body = "The cat sat on the mat."
    Set-EmailWordReplacements -Body $body -Replace 'cat' -ReplaceWith 'dog' -RegEx
    # This will replace "cat" with "dog" using regular expressions in the email body.

    #>
    if ($RegEx) {
        $Body = $Body -Replace $Replace, $ReplaceWith
    } else {
        $Body = $Body.Replace($Replace, $ReplaceWith)
    }
    return $Body
}
