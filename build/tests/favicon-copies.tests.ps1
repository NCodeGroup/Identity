#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/favicon-copies.ps1 against fixture kits and apps and against the repository's own kit and apps.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checker = Join-Path $PSScriptRoot '..' 'favicon-copies.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-favicon-copies-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function Write-FixtureFile([string]$Path, [byte[]]$Bytes) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllBytes($Path, $Bytes)
}

# Builds a kit folder and an apps folder under a named fixture and returns both paths. Each hashtable maps a relative
# path to the file's bytes.
function New-Fixture([string]$Name, [hashtable]$KitFiles, [hashtable]$AppFiles) {
    $kit = Join-Path $fixtureRoot $Name 'brand' 'favicon'
    $apps = Join-Path $fixtureRoot $Name 'frontend' 'apps'
    $null = [System.IO.Directory]::CreateDirectory($kit)
    $null = [System.IO.Directory]::CreateDirectory($apps)
    foreach ($relative in $KitFiles.Keys) {
        Write-FixtureFile (Join-Path $kit $relative) $KitFiles[$relative]
    }
    foreach ($relative in $AppFiles.Keys) {
        Write-FixtureFile (Join-Path $apps $relative) $AppFiles[$relative]
    }
    return @{ Kit = $kit; Apps = $apps }
}

function Assert-Problems([string]$Name, [string]$Kit, [string]$Apps, [string[]]$ExpectedFragments) {
    $problems = @(& $checker -Kit $Kit -Apps $Apps)
    if ($problems.Count -ne $ExpectedFragments.Count) {
        throw "Test '$Name' expected $($ExpectedFragments.Count) problem(s), got $($problems.Count): $($problems -join ' | ')"
    }
    for ($i = 0; $i -lt $ExpectedFragments.Count; $i++) {
        if (-not $problems[$i].Contains($ExpectedFragments[$i], [StringComparison]::Ordinal)) {
            throw "Test '$Name' expected problem $i to contain '$($ExpectedFragments[$i])', got '$($problems[$i])'."
        }
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

$ico = [byte[]](0, 0, 1, 0, 1, 0)
$svg = [System.Text.Encoding]::UTF8.GetBytes('<svg xmlns="http://www.w3.org/2000/svg"/>')
$other = [System.Text.Encoding]::UTF8.GetBytes('<svg xmlns="http://www.w3.org/2000/svg" id="other"/>')

try {
    $f = New-Fixture 'equal' @{
        'favicon.ico' = $ico
        'favicon.svg' = $svg
    } @{
        'a/favicon/favicon.ico' = $ico
        'a/favicon/favicon.svg' = $svg
        'b/favicon/favicon.ico' = $ico
        'b/favicon/favicon.svg' = $svg
    }
    Assert-Problems 'equal copies have no problems' $f.Kit $f.Apps @()

    $f = New-Fixture 'differs' @{
        'favicon.ico' = $ico
        'favicon.svg' = $svg
    } @{
        'a/favicon/favicon.ico' = $ico
        'a/favicon/favicon.svg' = $other
    }
    Assert-Problems 'a copy with other bytes is reported' $f.Kit $f.Apps @(
        'apps/a/favicon/favicon.svg: differs'
    )

    $f = New-Fixture 'extra' @{
        'favicon.ico' = $ico
    } @{
        'a/favicon/favicon.ico' = $ico
        'a/favicon/extra.png' = $ico
    }
    Assert-Problems 'a file the kit lacks is reported' $f.Kit $f.Apps @(
        'apps/a/favicon/extra.png: brand/favicon/ has no'
    )

    $f = New-Fixture 'no-folder' @{
        'favicon.ico' = $ico
    } @{
        'a/index.html' = [System.Text.Encoding]::UTF8.GetBytes('<html></html>')
    }
    Assert-Problems 'an app with no favicon folder has no problems' $f.Kit $f.Apps @()

    $f = New-Fixture 'ordered' @{
        'favicon.ico' = $ico
        'favicon.svg' = $svg
    } @{
        'b/favicon/favicon.ico' = $other
        'a/favicon/favicon.svg' = $other
        'a/favicon/favicon.ico' = $other
    }
    Assert-Problems 'problems are reported by app and file in ordinal order' $f.Kit $f.Apps @(
        'apps/a/favicon/favicon.ico: differs',
        'apps/a/favicon/favicon.svg: differs',
        'apps/b/favicon/favicon.ico: differs'
    )

    Assert-Problems 'the repository kit and apps have no problems' `
        (Join-Path $PSScriptRoot '..' '..' 'brand' 'favicon') `
        (Join-Path $PSScriptRoot '..' '..' 'frontend' 'apps') @()

    Write-Host "$testCount favicon-copies checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
