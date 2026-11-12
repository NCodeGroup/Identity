#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks that build/openapi.ps1 refuses a malformed version before it builds or generates anything.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot '..' 'openapi.ps1'
$testCount = 0

# A child process, so the script's strict mode, return and throw stay in it. A wrapped error is joined onto one line.
function Invoke-OpenApi([string[]]$Arguments) {
    $output = & pwsh -NoProfile -File $script @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -replace '\r?\n\s+\|\s?', ' ').Trim() }
}

function Pass([string]$Name) {
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Assert-Throws([string]$Name, [string[]]$Arguments, [string]$Fragment) {
    $run = Invoke-OpenApi $Arguments
    if ($run.ExitCode -eq 0 -or -not $run.Output.Contains($Fragment, [StringComparison]::Ordinal)) {
        throw "Test '$Name' expected an error containing '$Fragment', got exit code $($run.ExitCode): $($run.Output)"
    }
    Pass $Name
}

Assert-Throws 'a version with a space is refused' @('-NoBuild', '-Version', 'bad version') 'is not a version'
Assert-Throws 'a shell character is refused' @('-NoBuild', '-Version', '1.0;rm') 'is not a version'

Write-Host "OpenAPI: $testCount tests passed."
