#requires -Version 7

<#
.SYNOPSIS
    Functions the conformance scripts share; dot-source it (`. "$PSScriptRoot/ConformanceHelpers.ps1"`).
#>

function ConvertTo-Base64Url([byte[]] $Bytes) {
    # Base64url without padding (RFC 4648 §5), the encoding of the development secret files and of JSON Web Keys.
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Expand-ConfigTemplate([string] $TemplateText, [System.Collections.IDictionary] $Tokens) {
    # Replace each {{token}} with its JSON-escaped value; the template quotes every token, so the result stays JSON. A
    # token the caller does not supply would reach the suite as literal text, so any that remains fails the expansion.
    $text = $TemplateText
    foreach ($token in $Tokens.GetEnumerator()) {
        $text = $text.Replace("{{$($token.Key)}}", [System.Text.Json.JsonEncodedText]::Encode($token.Value).ToString())
    }
    $remaining = @([regex]::Matches($text, '\{\{[^{}]*\}\}') | ForEach-Object Value | Select-Object -Unique)
    if ($remaining.Count -gt 0) {
        throw "The configuration template has tokens that were not supplied: $($remaining -join ', ')."
    }
    return $text
}
