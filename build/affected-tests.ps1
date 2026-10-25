#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Selects the test projects affected by the changes on the current branch.

.DESCRIPTION
    Collects the changed paths (committed, staged and unstaged changes since the merge base of HEAD and -Base, plus
    untracked files), maps each path to the project under src/ or tests/ whose directory is its longest prefix, and
    selects the test projects (under tests/) in the reverse ProjectReference closure of those projects. A change to a
    global build input (build/, .config/, global.json, nuget.config, version.json, the solution, .editorconfig,
    BannedSymbols.txt, any Directory.* file, any .runsettings file) selects all tests. A path under no project, such as
    docs/, charts/, src/frontend/ or .github/, selects nothing (ADR-0263).

    Outputs one object with All (true when a global trigger matched), Projects (repository-relative test .csproj paths
    with / separators, sorted ordinal, empty when All is true) and Reasons (one line per changed path).

.PARAMETER Root
    The repository root. Defaults to the parent of the build/ directory.

.PARAMETER Base
    The commit-ish whose merge base with HEAD the changes are measured from. Defaults to origin/main.

.PARAMETER ChangedPath
    Repository-relative paths to use as the change set instead of asking git. When supplied, git is not called.

.EXAMPLE
    ./build/affected-tests.ps1

.EXAMPLE
    ./build/affected-tests.ps1 -ChangedPath 'src/NCode.Time/TimeProviderExtensions.cs'
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Base = 'origin/main',
    [string[]]$ChangedPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = [IO.Path]::GetFullPath($Root)
$globalTrigger = '^(build/|\.config/|global\.json$|nuget\.config$|version\.json$|NCode\.Identity\.slnx$|\.editorconfig$|BannedSymbols\.txt$)|(^|/)Directory\.[^/]+$|\.runsettings$'

function ConvertTo-RelativePath([string]$FullPath) {
    return [IO.Path]::GetRelativePath($Root, $FullPath).Replace('\', '/')
}

function Invoke-Git {
    param([string[]]$Arguments, [string]$Failure)
    $output = & git -C $Root @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $Failure
    }
    return [string[]]@($output)
}

if ($PSBoundParameters.ContainsKey('ChangedPath')) {
    $rawPaths = [string[]]@($ChangedPath)
}
else {
    $baseFailure = "Cannot find the merge base of HEAD and '$Base'. Fetch it first (git fetch origin)."
    $mergeBase = (Invoke-Git -Arguments @('merge-base', 'HEAD', $Base) -Failure $baseFailure | Select-Object -First 1)
    if (-not $mergeBase) {
        throw $baseFailure
    }
    $diffPaths = Invoke-Git -Arguments @('diff', '--name-only', '--no-renames', $mergeBase.Trim()) `
        -Failure "git diff against '$mergeBase' failed."
    $untrackedPaths = Invoke-Git -Arguments @('ls-files', '--others', '--exclude-standard') `
        -Failure 'git ls-files failed.'
    $rawPaths = [string[]]@(@($diffPaths) + @($untrackedPaths))
}

$seenPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$changedPaths = [System.Collections.Generic.List[string]]::new()
foreach ($rawPath in $rawPaths) {
    if ([string]::IsNullOrWhiteSpace($rawPath)) {
        continue
    }
    $path = $rawPath.Trim().Replace('\', '/')
    if ($seenPaths.Add($path)) {
        $changedPaths.Add($path)
    }
}

# Every project under src/ and tests/, keyed by its repository-relative path, with its directory and references.
$projects = [ordered]@{}
foreach ($folder in @('src', 'tests')) {
    $folderPath = Join-Path $Root $folder
    if (-not (Test-Path -LiteralPath $folderPath)) {
        continue
    }
    foreach ($file in Get-ChildItem -LiteralPath $folderPath -Recurse -File -Filter '*.csproj') {
        $relativePath = ConvertTo-RelativePath $file.FullName
        if ($relativePath -match '/(bin|obj)/') {
            continue
        }
        [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw
        $references = [System.Collections.Generic.List[string]]::new()
        foreach ($node in $xml.SelectNodes('//ProjectReference')) {
            $include = $node.GetAttribute('Include')
            if ([string]::IsNullOrWhiteSpace($include)) {
                continue
            }
            $referencePath = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $include.Replace('\', '/')))
            $references.Add((ConvertTo-RelativePath $referencePath))
        }
        $projects[$relativePath] = [pscustomobject]@{
            Directory  = (ConvertTo-RelativePath $file.DirectoryName) + '/'
            References = $references
        }
    }
}

# The reverse reference map: each project to the projects that reference it.
$referencedBy = @{}
foreach ($project in $projects.Keys) {
    foreach ($reference in $projects[$project].References) {
        if (-not $referencedBy.ContainsKey($reference)) {
            $referencedBy[$reference] = [System.Collections.Generic.List[string]]::new()
        }
        $referencedBy[$reference].Add($project)
    }
}

$all = $false
$reasons = [System.Collections.Generic.List[string]]::new()
$mappedProjects = [System.Collections.Generic.List[string]]::new()
foreach ($path in $changedPaths) {
    if ($path -match $globalTrigger) {
        $all = $true
        $reasons.Add("$path -> all tests (global trigger)")
        continue
    }
    $owner = $null
    $ownerLength = -1
    foreach ($project in $projects.Keys) {
        $directory = $projects[$project].Directory
        if ($path.StartsWith($directory, [StringComparison]::Ordinal) -and $directory.Length -gt $ownerLength) {
            $owner = $project
            $ownerLength = $directory.Length
        }
    }
    if ($owner) {
        $mappedProjects.Add($owner)
        $reasons.Add("$path -> $owner")
    }
    else {
        $reasons.Add("$path -> nothing (no project)")
    }
}

$selected = [string[]]@()
if (-not $all) {
    $visited = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $queue = [System.Collections.Generic.Queue[string]]::new()
    foreach ($project in $mappedProjects) {
        if ($visited.Add($project)) {
            $queue.Enqueue($project)
        }
    }
    while ($queue.Count -gt 0) {
        $project = $queue.Dequeue()
        if ($referencedBy.ContainsKey($project)) {
            foreach ($dependent in $referencedBy[$project]) {
                if ($visited.Add($dependent)) {
                    $queue.Enqueue($dependent)
                }
            }
        }
    }
    $selected = [string[]]@($visited | Where-Object { $_.StartsWith('tests/', [StringComparison]::Ordinal) })
    [Array]::Sort($selected, [StringComparer]::Ordinal)
}

[pscustomobject]@{
    All      = $all
    Projects = $selected
    Reasons  = [string[]]@($reasons)
}
