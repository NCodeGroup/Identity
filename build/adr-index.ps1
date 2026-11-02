#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks the ADR index in docs/decisions/README.md against the ADR files on disk, and checks that tracked files link
    only to decision records that exist.

.DESCRIPTION
    The index is maintained by hand, and hand-maintenance demonstrably fails: ten ADRs once sat unindexed, a malformed
    delimiter row (which silently degrades the whole table to a paragraph) survived dozens of commits, and plans run in
    parallel worktrees can each take "the next free number" and merge two ADRs with one number (issue #371). An
    unindexed or ambiguous ADR is one nobody finds, which defeats the point of writing it (project-template ADR-0001).

    Outputs one string per problem, and nothing when the index is consistent:
    - a table row whose column count is not 3;
    - two ADR files that share a four-digit number, naming both;
    - an ADR file numbered below 1000, the range the project template's records use (project-template ADR-0012);
    - an ADR file that the index does not list, or an index link to a file that does not exist;
    - an index row whose link text number is not the number of the file it links;
    - a file listed by more than one index row;
    - a tracked file that names a NNNN-<slug>.md record found in neither docs/decisions/ nor
      docs/decisions/project-template/ (a link that a rename or renumber broke);
    - a tracked file that points a docs/decisions/0NNN path at docs/decisions/, because the family's records are
      numbered from 1000 and the project template's records live in docs/decisions/project-template/.

    0000-template.md is the template, not a decision, so it is deliberately absent from the index, and the one
    docs/decisions/ path numbered below 1000 that a link may use.

.PARAMETER Root
    The repository root, whose docs/decisions folder holds README.md and the ADR files. Defaults to the folder above
    this script's.

.EXAMPLE
    ./build/adr-index.ps1
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Directory = Join-Path $Root 'docs/decisions'

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

$onDisk = @(Get-ChildItem -LiteralPath $Directory -Filter '*.md' -File |
        Where-Object { $_.Name -match '^(?!0000-)\d{4}-' } |
        ForEach-Object { $_.Name } |
        Sort-Object -Culture ([cultureinfo]::InvariantCulture))

# Each ADR is cited by its number alone ("ADR-1278"), so two files with one number make every such citation ambiguous.
$onDisk |
    Group-Object { $_.Substring(0, 4) } |
    Where-Object { $_.Count -gt 1 } |
    ForEach-Object {
        $problems.Add("ADR number $($_.Name) is used by more than one file: $(($_.Group | ForEach-Object { "docs/decisions/$_" }) -join ', ').")
    }

# Numbers below 1000 are the project template's (project-template ADR-0012), so a record numbered there would share
# its citation with a template record.
foreach ($file in $onDisk | Where-Object { [int]$_.Substring(0, 4) -lt 1000 }) {
    $problems.Add("docs/decisions/$file is numbered below 1000, the project template's range; number it from 1000.")
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

# Searches the tracked text files (never bin/, obj/ or node_modules/, which are untracked) for a pattern. The checker's
# own test file is excluded because its fixtures name records that exist only in the fixtures. A line that repeats a
# match (an index row names its file in the link and in the title) is reported once.
function Find-TrackedText([string]$Pattern) {
    $output = @(git -C $Root grep -I -n -o -z -E $Pattern -- . ':(exclude)build/tests/adr-index.tests.ps1')
    if ($LASTEXITCODE -gt 1) {
        throw "git grep failed with exit code $LASTEXITCODE."
    }
    $global:LASTEXITCODE = 0
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($line in $output) {
        $path, $number, $text = ([string]$line).Split([char]0, 3)
        if ($seen.Add("$path|$number|$text")) {
            [pscustomobject]@{ Path = $path; Line = $number; Text = $text }
        }
    }
}

# A link that a rename or renumber broke is otherwise found only by a reader who follows it. Records are looked up by
# file name, in this folder and in the project template's.
$known = @(foreach ($folder in $Directory, (Join-Path $Directory 'project-template')) {
        if (Test-Path -LiteralPath $folder -PathType Container) {
            Get-ChildItem -LiteralPath $folder -Filter '*.md' -File | ForEach-Object { $_.Name }
        }
    })
foreach ($match in Find-TrackedText '[0-9]{4}-[a-z0-9][a-z0-9-]*\.md') {
    if ($match.Text -notin $known) {
        $problems.Add("$($match.Path)($($match.Line)) names $($match.Text), which is in neither docs/decisions/ nor docs/decisions/project-template/.")
    }
}

# The family's records are numbered from 1000 and the template's live in project-template/, so a docs/decisions/0NNN
# path is a template record linked at the wrong folder; only the template (0000-template.md) is in this folder.
foreach ($match in Find-TrackedText 'docs/decisions/0[0-9]{3}[a-z0-9.-]*') {
    if ($match.Text -cne 'docs/decisions/0000-template.md') {
        $problems.Add("$($match.Path)($($match.Line)) points $($match.Text) at docs/decisions/, where the family's records are numbered from 1000; the template's records are in docs/decisions/project-template/.")
    }
}

$problems
