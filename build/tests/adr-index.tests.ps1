#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/adr-index.ps1 against fixture repositories and against this repository.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checker = Join-Path $PSScriptRoot '..' 'adr-index.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-adr-index-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function Write-FixtureFile([string]$Path, [string]$Text) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

# Builds an ADR directory with the given files and index rows; each row is "<number>|<file>".
function New-Fixture([string]$Name, [string[]]$Files, [string[]]$Rows) {
    $root = Join-Path $fixtureRoot $Name
    foreach ($file in @($Files) + '0000-template.md') {
        Write-FixtureFile (Join-Path $root 'docs' 'decisions' $file) "# $file`n"
    }
    $lines = @('# Decisions', '', '| ADR | Title | Status |', '| --- | --- | --- |')
    foreach ($row in $Rows) {
        $number, $file = $row.Split('|')
        $lines += "| [$number](./$file) | $file | Accepted |"
    }
    Write-FixtureFile (Join-Path $root 'docs' 'decisions' 'README.md') (($lines -join "`n") + "`n")
    git -C $root init --quiet | Out-Null
    git -C $root -c core.autocrlf=false add --all 2>$null | Out-Null
    return $root
}

function Add-FixtureFile([string]$Root, [string]$Path, [string]$Text) {
    Write-FixtureFile (Join-Path $Root $Path) $Text
    git -C $Root -c core.autocrlf=false add --all 2>$null | Out-Null
}

function Assert-Problems([string]$Name, [string]$Root, [string[]]$ExpectedFragments) {
    $problems = @(& $checker -Root $Root)
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

try {
    $fixture = New-Fixture 'consistent' @('1001-a.md', '1002-b.md') @('1001|1001-a.md', '1002|1002-b.md')
    Assert-Problems 'a consistent index has no problems' $fixture @()

    $fixture = New-Fixture 'duplicate' @('1001-a.md', '1001-b.md') @('1001|1001-a.md', '1001|1001-b.md')
    Assert-Problems 'two files with one number are named' $fixture @(
        'ADR number 1001 is used by more than one file: docs/decisions/1001-a.md, docs/decisions/1001-b.md.'
    )

    $fixture = New-Fixture 'unindexed' @('1001-a.md', '1002-b.md') @('1001|1001-a.md')
    Assert-Problems 'an unindexed file is reported' $fixture @('docs/decisions/1002-b.md is not listed')

    $fixture = New-Fixture 'missing' @('1001-a.md') @('1001|1001-a.md', '1002|1002-b.md')
    Assert-Problems 'a link to a missing file is reported' $fixture @(
        'links ./1002-b.md, which does not exist'
        'docs/decisions/README.md(6) names 1002-b.md, which is in neither'
    )

    $fixture = New-Fixture 'mislabeled' @('1001-a.md', '1002-b.md') @('1001|1001-a.md', '1003|1002-b.md')
    Assert-Problems 'a row labeled with another number is reported' $fixture @('labels ./1002-b.md as 1003')

    $fixture = New-Fixture 'twice' @('1001-a.md') @('1001|1001-a.md', '1001|1001-a.md')
    Assert-Problems 'a file listed twice is reported' $fixture @('lists ./1001-a.md on more than one row')

    $fixture = New-Fixture 'from-1000' @('1000-a.md') @()
    Assert-Problems 'a record numbered from 1000 is checked' $fixture @('docs/decisions/1000-a.md is not listed')

    $fixture = New-Fixture 'below-1000' @('0999-a.md') @('0999|0999-a.md')
    Assert-Problems 'a record numbered below 1000 is reported' $fixture @('docs/decisions/0999-a.md is numbered below 1000')

    $fixture = New-Fixture 'columns'@('1001-a.md') @('1001|1001-a.md')
    Add-Content -LiteralPath (Join-Path $fixture 'docs' 'decisions' 'README.md') -Value '| [x] | two |'
    Assert-Problems 'a row with the wrong column count is reported' $fixture @('table row has 2 columns, expected 3')

    $fixture = New-Fixture 'link-missing' @('1001-a.md') @('1001|1001-a.md')
    Add-FixtureFile $fixture 'notes.md' "See [ADR-1003](docs/decisions/1003-c.md).`n"
    Assert-Problems 'a link to a missing record is reported' $fixture @('notes.md(1) names 1003-c.md, which is in neither')

    $fixture = New-Fixture 'link-template' @('1001-a.md') @('1001|1001-a.md')
    Add-FixtureFile $fixture 'docs/decisions/project-template/0001-t.md' "# 0001-t.md`n"
    Add-FixtureFile $fixture 'notes.md' "See [0001](docs/decisions/project-template/0001-t.md) and [the template](docs/decisions/0000-template.md).`n"
    Assert-Problems 'a link to a template record resolves' $fixture @()

    $fixture = New-Fixture 'link-family-path' @('1001-a.md') @('1001|1001-a.md')
    Add-FixtureFile $fixture 'docs/decisions/project-template/0001-t.md' "# 0001-t.md`n"
    Add-FixtureFile $fixture 'notes.md' "See [0001](docs/decisions/0001-t.md).`n"
    Assert-Problems 'a family path with a template number is reported' $fixture @('notes.md(1) points docs/decisions/0001-t.md at docs/decisions/')

    Assert-Problems 'the repository has no problems' (Join-Path $PSScriptRoot '..' '..') @()

    Write-Host "$testCount adr-index checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
