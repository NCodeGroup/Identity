#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks that no /// doc comment under src/ cites a decision record.

.DESCRIPTION
    A /// comment ships in the package's XML documentation and, for HTTP contracts, in the OpenAPI document, where an
    ADR number or a docs/decisions/ link means nothing to a consumer. The citation belongs in a // comment, which never
    ships (csharp-production.instructions.md, "No internal citations in /// doc comments").

    Outputs one string per problem, and nothing when clean:
    - a /// line that matches ADR-NNNN or docs/decisions/.

    Files under a bin or obj folder are generated, so they are skipped. Test projects ship no XML documentation, so
    only src/ is scanned.

.PARAMETER Root
    The folder to scan. Defaults to src under the repository root.

.EXAMPLE
    ./build/doc-comment-citations.ps1
#>
[CmdletBinding()]
param(
    [string]$Root = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$problems = [System.Collections.Generic.List[string]]::new()
$base = Split-Path -Parent $Root

$paths = [System.Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.cs') {
    $inRoot = [IO.Path]::GetRelativePath($Root, $file.FullName)
    if ($inRoot -notmatch '(^|[\\/])(bin|obj)[\\/]') {
        $paths.Add($file.FullName)
    }
}
$sorted = $paths.ToArray()
[Array]::Sort($sorted, [StringComparer]::Ordinal)

foreach ($path in $sorted) {
    $relative = [IO.Path]::GetRelativePath($base, $path).Replace('\', '/')
    foreach ($match in Select-String -LiteralPath $path -Pattern '^\s*///.*(ADR-\d{4}|docs/decisions/)') {
        $problems.Add("$relative($($match.LineNumber)): a /// doc comment cites a decision record; keep the prose in /// and move the citation to a // comment.")
    }
}

$problems
