#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/stamp-version.ps1 against fixture copies of the repository's version.json and against malformed input.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot '..' 'stamp-version.ps1'
$repositoryVersionJson = Join-Path $PSScriptRoot '..' '..' 'version.json'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-stamp-version-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function New-Fixture([string]$Name, [string]$Content) {
    $null = [System.IO.Directory]::CreateDirectory($fixtureRoot)
    $path = Join-Path $fixtureRoot "$Name.json"
    [System.IO.File]::WriteAllText($path, $Content)
    return $path
}

function Get-Version([string]$Path) {
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json).version
}

# A child process, so the script's strict mode, return and throw stay in it. A wrapped error is joined onto one line.
function Invoke-StampVersion([string[]]$Arguments) {
    $output = & pwsh -NoProfile -File $script @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -replace '\r?\n\s+\|\s?', ' ').Trim() }
}

function Pass([string]$Name) {
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Assert-Stamps([string]$Name, [string]$Path, [string]$Version) {
    $run = Invoke-StampVersion @('-Version', $Version, '-VersionJson', $Path)
    if ($run.ExitCode -ne 0) {
        throw "Test '$Name' expected exit code 0, got exit code $($run.ExitCode): $($run.Output)"
    }
    $actual = Get-Version $Path
    if ($actual -cne $Version) {
        throw "Test '$Name' expected version '$Version' in the file, got '$actual'."
    }
    Pass $Name
}

function Assert-Throws([string]$Name, [string[]]$Arguments, [string]$Fragment) {
    $run = Invoke-StampVersion $Arguments
    if ($run.ExitCode -eq 0 -or -not $run.Output.Contains($Fragment, [StringComparison]::Ordinal)) {
        throw "Test '$Name' expected an error containing '$Fragment', got exit code $($run.ExitCode): $($run.Output)"
    }
    Pass $Name
}

try {
    $original = [System.IO.File]::ReadAllText($repositoryVersionJson)
    $originalVersion = Get-Version $repositoryVersionJson
    $originalSpecs = @((ConvertFrom-Json $original).publicReleaseRefSpec).Count

    $copy = New-Fixture 'alpha' $original
    Assert-Stamps 'stamp a prerelease' $copy '0.0.1-alpha.57'
    $specs = @((Get-Content -LiteralPath $copy -Raw | ConvertFrom-Json).publicReleaseRefSpec).Count
    if ($specs -ne $originalSpecs) {
        throw "Test 'stamp keeps the other settings' expected $originalSpecs publicReleaseRefSpec entries, got $specs."
    }
    Pass 'stamp keeps the other settings'

    Assert-Stamps 'stamp a stable version' $copy '1.0.3'

    $same = New-Fixture 'same' $original
    Assert-Stamps 'stamp the file''s own version' $same $originalVersion

    $untouched = New-Fixture 'untouched' $original
    Assert-Throws 'build metadata is refused' @('-Version', '1.0.0+build', '-VersionJson', $untouched) 'is not a version'
    Assert-Throws 'a shell character is refused' @('-Version', '1.0;id', '-VersionJson', $untouched) 'is not a version'
    if ((Get-Version $untouched) -cne $originalVersion) {
        throw "Test 'a refused version leaves the file' expected '$originalVersion', got '$(Get-Version $untouched)'."
    }
    Pass 'a refused version leaves the file'

    Assert-Throws 'no version is refused' @('-VersionJson', $untouched) 'Give the version'

    $missing = New-Fixture 'missing' '{"assemblyVersion":{"precision":"major"}}'
    Assert-Throws 'a file without a version is refused' @('-Version', '1.0.3', '-VersionJson', $missing) 'found 0'

    $nested = New-Fixture 'nested' '{"inner":{"version":"1.0"}}'
    Assert-Throws 'a nested version is refused' @('-Version', '1.0.3', '-VersionJson', $nested) 'after stamping'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host "Stamp version: $testCount tests passed."
