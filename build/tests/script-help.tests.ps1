#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks that every build script with a comment-based help block exposes it to Get-Help.

.DESCRIPTION
    PowerShell reads line comments that touch a <# help block as part of it, so a shebang or #requires line directly
    above the block hides the help. Each build/*.ps1 that has a <# block must yield a synopsis from its parsed help.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scripts = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..') -Filter '*.ps1' -File
$scripts += Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File
$failures = [System.Collections.Generic.List[string]]::new()
$checked = 0

foreach ($script in $scripts) {
    if (-not (Select-String -LiteralPath $script.FullName -Pattern '^<#' -Quiet)) {
        continue
    }
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$errors)
    $help = $ast.GetHelpContent()
    if (-not $help -or [string]::IsNullOrWhiteSpace($help.Synopsis)) {
        $failures.Add("$($script.Name): its help block does not parse; keep a blank line between #requires and <#.")
    }
    $checked++
}

if ($failures.Count -gt 0) {
    throw ($failures -join [Environment]::NewLine)
}
Write-Host "Build script help: $checked scripts checked."
