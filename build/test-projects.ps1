#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Lists the repository's test projects by tier.

.DESCRIPTION
    Finds every *.csproj under the tests/ folder, skipping bin/ and obj/ folders. A project whose file name ends with
    .EndToEnd.Tests.csproj belongs to the end-to-end tier; every other project is a test project.

    Outputs one object with Tests (repository-relative .csproj paths of the test projects) and EndToEnd
    (repository-relative .csproj paths of the end-to-end projects). Paths use / separators and each list is sorted
    ordinal.

.PARAMETER Root
    The repository root. Defaults to the parent of the build/ directory.

.EXAMPLE
    ./build/test-projects.ps1
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = [IO.Path]::GetFullPath($Root)
$testsFolder = Join-Path $Root 'tests'

$tests = [System.Collections.Generic.List[string]]::new()
$endToEnd = [System.Collections.Generic.List[string]]::new()

if (Test-Path -LiteralPath $testsFolder -PathType Container) {
    foreach ($file in Get-ChildItem -LiteralPath $testsFolder -Recurse -File -Filter '*.csproj') {
        $relativePath = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        if ($relativePath -match '/(bin|obj)/') {
            continue
        }
        if ($file.Name.EndsWith('.EndToEnd.Tests.csproj', [StringComparison]::Ordinal)) {
            $endToEnd.Add($relativePath)
        }
        else {
            $tests.Add($relativePath)
        }
    }
}

$tests.Sort([StringComparer]::Ordinal)
$endToEnd.Sort([StringComparer]::Ordinal)

[pscustomobject]@{
    Tests    = [string[]]$tests.ToArray()
    EndToEnd = [string[]]$endToEnd.ToArray()
}
