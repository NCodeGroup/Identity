#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/doc-comment-citations.ps1 against fixture source folders and against the repository's own src/, hosts/, analyzers/ and tests/.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checker = Join-Path $PSScriptRoot '..' 'doc-comment-citations.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-doc-comment-citations-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function Write-FixtureFile([string]$Path, [string]$Text) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

# Builds a folder (src by default) with the given files (relative path -> text) and returns its path.
function New-Fixture([string]$Name, [hashtable]$Files, [string]$Folder = 'src') {
    $root = Join-Path $fixtureRoot $Name $Folder
    $null = [System.IO.Directory]::CreateDirectory($root)
    foreach ($relative in $Files.Keys) {
        Write-FixtureFile (Join-Path $root $relative) $Files[$relative]
    }
    return $root
}

function Assert-Problems([string]$Name, [string[]]$Root, [string[]]$ExpectedFragments) {
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
    $fixture = New-Fixture 'clean' @{
        'A/Clean.cs' = "/// <summary>Names compare ordinally.</summary>`n// See ADR-1150.`npublic static class Clean { }`n"
    }
    Assert-Problems 'a clean doc comment has no problems' $fixture @()

    $fixture = New-Fixture 'adr' @{
        'A/Adr.cs' = "namespace A;`n/// <summary>`n/// Names compare ordinally (ADR-1150).`n/// </summary>`npublic static class Adr { }`n"
    }
    Assert-Problems 'an ADR number in a /// line is reported' $fixture @(
        'src/A/Adr.cs(3): a /// doc comment cites a decision record'
    )

    $fixture = New-Fixture 'link' @{
        'A/Link.cs' = "    /// <see href=`"../../docs/decisions/1088-x.md`">x</see>`n"
    }
    Assert-Problems 'a docs/decisions link in a /// line is reported' $fixture @('src/A/Link.cs(1):')

    $fixture = New-Fixture 'template' @{
        'A/Template.cs' = "/// See project-template ADR-0012.`n"
    }
    Assert-Problems 'a project-template record number is reported' $fixture @('src/A/Template.cs(1):')

    $fixture = New-Fixture 'line' @{
        'A/Line.cs' = "    // See ADR-1150 and docs/decisions/README.md.`n"
    }
    Assert-Problems 'a // comment that cites an ADR is allowed' $fixture @()

    $fixture = New-Fixture 'generated' @{
        'A/obj/Gen.cs' = "/// (ADR-1150)`n"
        'A/bin/Gen.cs' = "/// (ADR-1150)`n"
    }
    Assert-Problems 'generated files under bin and obj are skipped' $fixture @()

    $fixture = New-Fixture 'tests-adr' @{
        'A/AdrTests.cs' = "namespace A;`n/// <summary>`n/// Proves the rule (ADR-1150).`n/// </summary>`npublic class AdrTests { }`n"
    } 'tests'
    Assert-Problems 'an ADR number in a /// line under tests is reported' $fixture @(
        'tests/A/AdrTests.cs(3): a /// doc comment cites a decision record'
    )

    $src = New-Fixture 'both' @{
        'A/Adr.cs' = "/// (ADR-1150)`n"
    }
    $tests = New-Fixture 'both' @{
        'A/AdrTests.cs' = "/// (ADR-1150)`n"
    } 'tests'
    Assert-Problems 'src and tests are reported folder by folder' @($src, $tests) @(
        'src/A/Adr.cs(1):',
        'tests/A/AdrTests.cs(1):'
    )

    Assert-Problems 'the repository source and tests have no problems' @(
        (Join-Path $PSScriptRoot '..' '..' 'src'),
        (Join-Path $PSScriptRoot '..' '..' 'hosts'),
        (Join-Path $PSScriptRoot '..' '..' 'analyzers'),
        (Join-Path $PSScriptRoot '..' '..' 'tests')
    ) @()

    Write-Host "$testCount doc-comment-citations checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
