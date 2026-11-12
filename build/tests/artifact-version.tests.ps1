#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/artifact-version.ps1 against fixture version files and against the repository's own version.json.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot '..' 'artifact-version.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-artifact-version-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function New-VersionFile([string]$Name, [string]$Version) {
    $null = [System.IO.Directory]::CreateDirectory($fixtureRoot)
    $path = Join-Path $fixtureRoot "$Name.json"
    [System.IO.File]::WriteAllText($path, "{`"version`":`"$Version`"}")
    return $path
}

function Assert-Equal([string]$Name, [string]$Expected, [string]$Actual) {
    if ($Expected -cne $Actual) {
        throw "Test '$Name' expected '$Expected', got '$Actual'."
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Assert-Throws([string]$Name, [scriptblock]$Action, [string]$Fragment) {
    $message = $null
    try {
        & $Action | Out-Null
    }
    catch {
        $message = $_.Exception.Message
    }
    if ($null -eq $message) {
        throw "Test '$Name' expected an error, got none."
    }
    if (-not $message.Contains($Fragment, [StringComparison]::Ordinal)) {
        throw "Test '$Name' expected the error to contain '$Fragment', got '$message'."
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

try {
    $alpha = New-VersionFile 'alpha' '0.0.1-alpha'
    Assert-Equal 'prerelease with height' '0.0.1-alpha.57' ([string](& $script -VersionJson $alpha -Height '57'))

    $rc = New-VersionFile 'rc' '1.0.0-rc.1'
    Assert-Equal 'dotted prerelease with height' '1.0.0-rc.1.9' ([string](& $script -VersionJson $rc -Height '9'))

    $release = New-VersionFile 'release' '1.0.0'
    Assert-Throws 'release version is refused' { & $script -VersionJson $release -Height '5' } 'prerelease'

    $garbage = New-VersionFile 'garbage' 'not-a-version'
    Assert-Throws 'malformed version is refused' { & $script -VersionJson $garbage -Height '5' } 'prerelease'

    Assert-Throws 'non-numeric height is refused' { & $script -VersionJson $alpha -Height 'abc' } 'positive integer'
    Assert-Throws 'zero height is refused' { & $script -VersionJson $alpha -Height '0' } 'positive integer'

    $nine = [System.Management.Automation.SemanticVersion]::Parse([string](& $script -VersionJson $alpha -Height '9'))
    $ten = [System.Management.Automation.SemanticVersion]::Parse([string](& $script -VersionJson $alpha -Height '10'))
    if (-not ($nine -lt $ten)) {
        throw "Test 'height orders as semantic version' expected $nine to sort before $ten."
    }
    $testCount++
    Write-Host 'PASS: height orders as semantic version'

    $own = [string](& $script -Height '1')
    $null = [System.Management.Automation.SemanticVersion]::Parse($own)
    $testCount++
    Write-Host "PASS: repository version.json gives a semantic version ($own)"
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host "Artifact version: $testCount tests passed."
