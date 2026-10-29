#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/adr-index.ps1 against fixture ADR directories and against the repository's own index.
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
        Write-FixtureFile (Join-Path $root $file) "# $file`n"
    }
    $lines = @('# Decisions', '', '| ADR | Title | Status |', '| --- | --- | --- |')
    foreach ($row in $Rows) {
        $number, $file = $row.Split('|')
        $lines += "| [$number](./$file) | $file | Accepted |"
    }
    Write-FixtureFile (Join-Path $root 'README.md') (($lines -join "`n") + "`n")
    return $root
}

function Assert-Problems([string]$Name, [string]$Directory, [string[]]$ExpectedFragments) {
    $problems = @(& $checker -Directory $Directory)
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
    $fixture = New-Fixture 'consistent' @('0001-a.md', '0002-b.md') @('0001|0001-a.md', '0002|0002-b.md')
    Assert-Problems 'a consistent index has no problems' $fixture @()

    $fixture = New-Fixture 'duplicate' @('0001-a.md', '0001-b.md') @('0001|0001-a.md', '0001|0001-b.md')
    Assert-Problems 'two files with one number are named' $fixture @(
        'ADR number 0001 is used by more than one file: docs/decisions/0001-a.md, docs/decisions/0001-b.md.'
    )

    $fixture = New-Fixture 'unindexed' @('0001-a.md', '0002-b.md') @('0001|0001-a.md')
    Assert-Problems 'an unindexed file is reported' $fixture @('docs/decisions/0002-b.md is not listed')

    $fixture = New-Fixture 'missing' @('0001-a.md') @('0001|0001-a.md', '0002|0002-b.md')
    Assert-Problems 'a link to a missing file is reported' $fixture @('links ./0002-b.md, which does not exist')

    $fixture = New-Fixture 'mislabeled' @('0001-a.md', '0002-b.md') @('0001|0001-a.md', '0003|0002-b.md')
    Assert-Problems 'a row labeled with another number is reported' $fixture @('labels ./0002-b.md as 0003')

    $fixture = New-Fixture 'twice' @('0001-a.md') @('0001|0001-a.md', '0001|0001-a.md')
    Assert-Problems 'a file listed twice is reported' $fixture @('lists ./0001-a.md on more than one row')

    $fixture = New-Fixture 'columns' @('0001-a.md') @('0001|0001-a.md')
    Add-Content -LiteralPath (Join-Path $fixture 'README.md') -Value '| [x] | two |'
    Assert-Problems 'a row with the wrong column count is reported' $fixture @('table row has 2 columns, expected 3')

    Assert-Problems 'the repository index has no problems' (Join-Path $PSScriptRoot '..' '..' 'docs' 'decisions') @()

    Write-Host "$testCount adr-index checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
