#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks the ADR index in docs/decisions/README.md against the ADR files on disk.

.DESCRIPTION
    The index is maintained by hand, and hand-maintenance demonstrably fails: ten ADRs once sat unindexed, a malformed
    delimiter row (which silently degrades the whole table to a paragraph) survived dozens of commits, and plans run in
    parallel worktrees can each take "the next free number" and merge two ADRs with one number (issue #371). An
    unindexed or ambiguous ADR is one nobody finds, which defeats the point of writing it (ADR-0008).

    Outputs one string per problem, and nothing when the index is consistent:
    - a table row whose column count is not 3;
    - two ADR files that share a four-digit number, naming both;
    - an ADR file that the index does not list, or an index link to a file that does not exist;
    - an index row whose link text number is not the number of the file it links;
    - a file listed by more than one index row.

    0000-template.md is the template, not a decision, so it is deliberately absent from the index.

.PARAMETER Directory
    The ADR directory that holds README.md and the ADR files. Defaults to docs/decisions under the repository root.

.EXAMPLE
    ./build/adr-index.ps1
#>
[CmdletBinding()]
param(
    [string]$Directory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/decisions')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$indexPath = Join-Path $Directory 'README.md'
$indexLines = @(Get-Content -LiteralPath $indexPath)
$problems = [System.Collections.Generic.List[string]]::new()

# A GitHub table renders only while every row has the same column count as its delimiter row. One row with the wrong
# count collapses the entire table into a run-on paragraph, so this is a correctness check, not cosmetics.
for ($i = 0; $i -lt $indexLines.Count; $i++) {
    if ($indexLines[$i].StartsWith('|')) {
        $columns = $indexLines[$i].TrimEnd().Split('|').Count - 2
        if ($columns -ne 3) {
            $problems.Add("docs/decisions/README.md($($i + 1)): table row has $columns columns, expected 3.")
        }
    }
}

$onDisk = @(Get-ChildItem -LiteralPath $Directory -Filter '0*.md' -File |
        Where-Object { $_.Name -match '^(?!0000-)\d{4}-' } |
        ForEach-Object { $_.Name } |
        Sort-Object -Culture ([cultureinfo]::InvariantCulture))

# Each ADR is cited by its number alone ("ADR-0278"), so two files with one number make every such citation ambiguous.
$onDisk |
    Group-Object { $_.Substring(0, 4) } |
    Where-Object { $_.Count -gt 1 } |
    ForEach-Object {
        $problems.Add("ADR number $($_.Name) is used by more than one file: $(($_.Group | ForEach-Object { "docs/decisions/$_" }) -join ', ').")
    }

$rows = @($indexLines |
        Select-String -Pattern '^\| \[(\d{4})\]\(\./([^)]+)\)' |
        ForEach-Object { [pscustomobject]@{ Line = $_.LineNumber; Number = $_.Matches[0].Groups[1].Value; File = $_.Matches[0].Groups[2].Value } })
$indexed = @($rows | ForEach-Object { $_.File })

foreach ($file in $onDisk | Where-Object { $_ -notin $indexed }) {
    $problems.Add("docs/decisions/$file is not listed in docs/decisions/README.md.")
}
foreach ($row in $rows) {
    if ($row.File -notin $onDisk) {
        $problems.Add("docs/decisions/README.md($($row.Line)) links ./$($row.File), which does not exist.")
    }
    elseif (-not $row.File.StartsWith("$($row.Number)-", [StringComparison]::Ordinal)) {
        $problems.Add("docs/decisions/README.md($($row.Line)) labels ./$($row.File) as $($row.Number).")
    }
}
$rows |
    Group-Object File |
    Where-Object { $_.Count -gt 1 } |
    ForEach-Object {
        $problems.Add("docs/decisions/README.md lists ./$($_.Name) on more than one row (lines $(($_.Group | ForEach-Object { $_.Line }) -join ', ')).")
    }

$problems
