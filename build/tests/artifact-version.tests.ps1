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

# A child process, so the script's strict mode, return and throw stay in it. A wrapped error is joined onto one line.
function Invoke-ArtifactVersion([string[]]$Arguments) {
    $output = & pwsh -NoProfile -File $script @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -replace '\r?\n\s+\|\s?', ' ').Trim() }
}

function Assert-Equal([string]$Name, [string]$Expected, [string[]]$Arguments) {
    $run = Invoke-ArtifactVersion $Arguments
    if ($run.ExitCode -ne 0 -or $run.Output -cne $Expected) {
        throw "Test '$Name' expected '$Expected' with exit code 0, got exit code $($run.ExitCode): $($run.Output)"
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Assert-Throws([string]$Name, [string[]]$Arguments, [string]$Fragment) {
    $run = Invoke-ArtifactVersion $Arguments
    if ($run.ExitCode -eq 0 -or -not $run.Output.Contains($Fragment, [StringComparison]::Ordinal)) {
        throw "Test '$Name' expected an error containing '$Fragment', got exit code $($run.ExitCode): $($run.Output)"
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

try {
    $alpha = New-VersionFile 'alpha' '0.0.1-alpha'
    Assert-Equal 'prerelease with height' '0.0.1-alpha.57' @('-VersionJson', $alpha, '-Height', '57')

    $rc = New-VersionFile 'rc' '1.0.0-rc.1'
    Assert-Equal 'dotted prerelease with height' '1.0.0-rc.1.9' @('-VersionJson', $rc, '-Height', '9')

    $release = New-VersionFile 'release' '1.0.0'
    Assert-Throws 'release version is refused' @('-VersionJson', $release, '-Height', '5') 'prerelease'

    $garbage = New-VersionFile 'garbage' 'not-a-version'
    Assert-Throws 'malformed version is refused' @('-VersionJson', $garbage, '-Height', '5') 'prerelease'

    Assert-Throws 'non-numeric height is refused' @('-VersionJson', $alpha, '-Height', 'abc') 'positive integer'
    Assert-Throws 'zero height is refused' @('-VersionJson', $alpha, '-Height', '0') 'positive integer'

    $nine = [System.Management.Automation.SemanticVersion]::Parse((Invoke-ArtifactVersion @('-VersionJson', $alpha, '-Height', '9')).Output)
    $ten = [System.Management.Automation.SemanticVersion]::Parse((Invoke-ArtifactVersion @('-VersionJson', $alpha, '-Height', '10')).Output)
    if (-not ($nine -lt $ten)) {
        throw "Test 'height orders as semantic version' expected $nine to sort before $ten."
    }
    $testCount++
    Write-Host 'PASS: height orders as semantic version'

    Assert-Equal 'tag with prerelease counter' '0.0.1-alpha.3' @('-VersionJson', $alpha, '-Tag', 'v0.0.1-alpha.3')
    Assert-Equal 'tag equal to the base prerelease' '0.0.1-alpha' @('-VersionJson', $alpha, '-Tag', 'v0.0.1-alpha')

    $line = New-VersionFile 'line' '1.0'
    Assert-Equal 'stable tag on the line' '1.0.3' @('-VersionJson', $line, '-Tag', 'v1.0.3')
    Assert-Throws 'tag off the line is refused' @('-VersionJson', $line, '-Tag', 'v1.1.0') 'not on the 1.0 line'
    Assert-Throws 'tag without v prefix is refused' @('-VersionJson', $line, '-Tag', '1.0.3') 'not a version tag'
    Assert-Throws 'tag with height is refused' @('-VersionJson', $line, '-Tag', 'v1.0.3', '-Height', '5') 'not both'

    $null = [System.Management.Automation.SemanticVersion]::Parse((Invoke-ArtifactVersion @('-VersionJson', $line, '-Tag', 'v1.0.3')).Output)
    $testCount++
    Write-Host 'PASS: tag version parses as semantic version'

    $own = (Invoke-ArtifactVersion @('-Height', '1')).Output
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
