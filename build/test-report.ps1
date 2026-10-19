#!/usr/bin/env pwsh
#requires -version 7
<#
.SYNOPSIS
    Presents JUnit test results: a console digest everywhere, plus a job summary and failure annotations on GitHub.

.DESCRIPTION
    Reads the JUnit XML reports a test run produced and presents them. The .NET JunitXml.TestLogger output and Vitest's
    junit reporter share the Ant JUnit shape, so one reporter serves both tiers and the same file feeds the uploaded
    artifact, the job summary, the annotations, and Codecov.

    This is reporting only: it never changes the caller's verdict. The test runner's own exit code stays the gate, and
    an unreadable report degrades to a warning instead of failing the run.

    On GitHub Actions (GITHUB_ACTIONS=true) it also appends one combined table plus the failed tests to the step's job
    summary, and emits an error annotation per failure — located at the first stack frame inside the repository — which
    GitHub shows on the run page and inline in pull-request diffs. GitHub keeps only the first 10 error annotations of
    a step, so the rest are listed in the summary alone.

.PARAMETER Path
    One or more JUnit report paths. Relative paths are resolved against -Root; PowerShell wildcards are allowed in any
    segment (for example 'src/frontend/*/*/test-results/junit.xml').

.PARAMETER Title
    The heading for this group of results, for example '.NET tests'.

.PARAMETER Root
    The repository root, used to resolve -Path and to make annotation paths repository-relative. Defaults to the parent
    of this script's directory.

.EXAMPLE
    ./build/test-report.ps1 -Path 'artifacts/test-results/*.junit.xml' -Title '.NET tests'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$Path,
    [string]$Title = 'Tests',
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$onGitHub = $env:GITHUB_ACTIONS -eq 'true'

# The backstop for "reporting only": a defect in this script surfaces as a warning (annotated on GitHub) rather than
# failing the caller's step, so a reporting bug can never turn a passing test run red.
trap {
    $location = "test-report.ps1:$($_.InvocationInfo.ScriptLineNumber)"
    $message = "The test report could not be produced ($location): $($_.Exception.Message)"
    Write-Warning $message
    if ($onGitHub) {
        Write-Host "::warning title=Test report::$($message.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A'))"
    }
    exit 0
}
$maxAnnotations = 10
$maxDetailedFailures = 50
$maxFailureText = 4000
$Root = [System.IO.Path]::GetFullPath($Root)

# Workflow-command escaping, as implemented by @actions/core: data escapes '%', CR, and LF; property values also
# escape ':' and ',', which would otherwise terminate the property.
function ConvertTo-CommandData([string]$Value) {
    $Value.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
}

function ConvertTo-CommandProperty([string]$Value) {
    (ConvertTo-CommandData $Value).Replace(':', '%3A').Replace(',', '%2C')
}

function ConvertTo-HtmlText([string]$Value) {
    [System.Net.WebUtility]::HtmlEncode($Value)
}

function ConvertTo-TableCell([string]$Value) {
    (ConvertTo-HtmlText $Value).Replace('|', '\|')
}

function Format-Count([int]$Value) {
    $Value.ToString('N0', $invariant)
}

function Format-Duration([double]$Seconds) {
    if ($Seconds -lt 60) {
        return $Seconds.ToString('0.0', $invariant) + ' s'
    }
    $span = [TimeSpan]::FromSeconds($Seconds)
    return '{0}m {1:00}s' -f [int][Math]::Floor($span.TotalMinutes), $span.Seconds
}

function Get-Truncated([string]$Value, [int]$Length) {
    if ($Value.Length -le $Length) {
        return $Value
    }
    return $Value.Substring(0, $Length) + "`n… (truncated)"
}

# A fenced block must open with more backticks than any run inside it, or the content would close the fence early.
function Get-CodeFence([string]$Content) {
    $longest = 0
    foreach ($match in [regex]::Matches($Content, '`+')) {
        $longest = [Math]::Max($longest, $match.Length)
    }
    return '`' * [Math]::Max(3, $longest + 1)
}

function Get-RepositoryRelativePath([string]$FullPath) {
    [System.IO.Path]::GetRelativePath($Root, $FullPath).Replace('\', '/')
}

# Resolves a path from a stack frame to an existing file inside the repository, or $null. Deterministic CI builds map
# source roots to '/_/' (SourceLink), relative frames (Vitest) are relative to the package that owns the report, and
# third-party frames (node_modules) are never the location a reader wants.
function Resolve-SourceFile([string]$Candidate, [string]$ReportDirectory) {
    if ([string]::IsNullOrWhiteSpace($Candidate) -or $Candidate -match '[\\/]node_modules[\\/]') {
        return $null
    }
    $candidate = $Candidate.Trim()
    if ($candidate.StartsWith('file:///')) {
        $candidate = [Uri]::new($candidate).LocalPath
    }

    $attempts = @()
    if ($candidate -match '^/_\d*/(?<rest>.+)$') {
        $attempts += Join-Path $Root $Matches.rest
    }
    elseif ([System.IO.Path]::IsPathRooted($candidate)) {
        $attempts += $candidate
    }
    else {
        $directory = $ReportDirectory
        while ($directory -and $directory.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
            $attempts += Join-Path $directory $candidate
            $directory = Split-Path -Parent $directory
        }
    }

    foreach ($attempt in $attempts) {
        try {
            $full = [System.IO.Path]::GetFullPath($attempt)
        }
        catch {
            continue
        }
        $insideRoot = $full.StartsWith($Root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
        if ($insideRoot -and (Test-Path -LiteralPath $full -PathType Leaf)) {
            return $full
        }
    }
    return $null
}

# The first in-repository frame of the failure (the test itself, or the production line that threw), falling back to
# the test file Vitest records as the class name. Returns @{ File; Line } with repository-relative File, or $null.
function Find-FailureLocation([string]$Text, [string]$ClassName, [string]$ReportDirectory) {
    $patterns = @(
        # .NET: "   at Namespace.Type.Method() in /path/File.cs:line 42"
        ' in (?<file>[^\r\n]+?):line (?<line>\d+)',
        # JavaScript/TypeScript: "❯ src/file.test.ts:12:5" or "at fn (/path/file.ts:12:5)"
        '(?<file>(?:file:///)?(?:[A-Za-z]:)?[^\s()''"<>|*?]+?\.(?:[cm]?[jt]sx?|vue|svelte)):(?<line>\d+)(?::\d+)?'
    )
    foreach ($pattern in $patterns) {
        foreach ($match in [regex]::Matches($Text, $pattern) | Select-Object -First 30) {
            $file = Resolve-SourceFile $match.Groups['file'].Value $ReportDirectory
            if ($file) {
                return @{ File = (Get-RepositoryRelativePath $file); Line = [int]$match.Groups['line'].Value }
            }
        }
    }
    $file = Resolve-SourceFile $ClassName $ReportDirectory
    if ($file) {
        return @{ File = (Get-RepositoryRelativePath $file); Line = $null }
    }
    return $null
}

function Test-SourceFileName([string]$Value) {
    $Value -match '[\\/]' -or $Value -match '\.[cm]?[jt]sx?$'
}

# .NET class names are type names ("Namespace.FooTests"); Vitest records the test file as the class name.
function Get-TestDisplayName([string]$ClassName, [string]$Name) {
    if ([string]::IsNullOrEmpty($ClassName)) {
        return $Name
    }
    if (Test-SourceFileName $ClassName) {
        return "$([System.IO.Path]::GetFileName($ClassName)) › $Name"
    }
    $shortClass = $ClassName.Substring($ClassName.LastIndexOf('.') + 1)
    return "$shortClass.$Name"
}

# A .NET report holds one suite named for its test assembly, which names the row. A Vitest report holds one suite per
# test file, so the row is named for the workspace package that owns the report (its package.json name), falling back
# to that directory's repository-relative path.
function Get-ReportLabel([System.Xml.XmlNodeList]$Suites, [System.IO.FileInfo]$Report) {
    if ($Suites.Count -eq 1) {
        $name = $Suites[0].GetAttribute('name')
        if ($name -and -not (Test-SourceFileName $name)) {
            return [regex]::Replace($name, '\.dll$', '')
        }
    }
    $directory = $Report.Directory
    if ($directory.Name -eq 'test-results' -and $directory.Parent) {
        $directory = $directory.Parent
    }
    $packageJson = Join-Path $directory.FullName 'package.json'
    if (Test-Path -LiteralPath $packageJson -PathType Leaf) {
        try {
            $package = Get-Content -LiteralPath $packageJson -Raw | ConvertFrom-Json
            if ($package.PSObject.Properties.Name -contains 'name' -and $package.name) {
                return [string]$package.name
            }
        }
        catch {
            Write-Verbose "Ignoring unreadable ${packageJson}: $($_.Exception.Message)"
        }
    }
    return Get-RepositoryRelativePath $directory.FullName
}

$reports = @(
    @(
        foreach ($pattern in $Path) {
            $resolved = if ([System.IO.Path]::IsPathRooted($pattern)) { $pattern } else { Join-Path $Root $pattern }
            Get-ChildItem -Path $resolved -File -ErrorAction SilentlyContinue
        }
    ) | Sort-Object FullName -Unique
)

$rows = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()

foreach ($report in $reports) {
    # An empty file is a run that never finished (a runner killed mid-suite creates the file before writing it). It
    # must be caught explicitly: an empty read is $null, and [xml]$null is $null rather than an exception.
    $xml = $null
    try {
        $content = Get-Content -LiteralPath $report.FullName -Raw
        if ([string]::IsNullOrWhiteSpace($content)) {
            throw 'the file is empty, so its test run did not finish'
        }
        $xml = [xml]$content
    }
    catch {
        $message = "Skipping unreadable JUnit report $(Get-RepositoryRelativePath $report.FullName): $($_.Exception.Message)"
        Write-Warning $message
        if ($onGitHub) {
            Write-Host "::warning title=$(ConvertTo-CommandProperty $Title)::$(ConvertTo-CommandData $message)"
        }
        continue
    }

    $label = Get-ReportLabel $xml.SelectNodes('//testsuite') $report
    $row = [pscustomobject]@{ Label = $label; Passed = 0; Failed = 0; Skipped = 0; Seconds = 0.0 }

    foreach ($case in $xml.SelectNodes('//testcase')) {
        $seconds = 0.0
        if ([double]::TryParse($case.GetAttribute('time'), [System.Globalization.NumberStyles]::Float, $invariant, [ref]$seconds)) {
            $row.Seconds += $seconds
        }

        $problem = $case.SelectSingleNode('failure|error')
        if ($problem) {
            $row.Failed++
            $className = $case.GetAttribute('classname')
            $message = $problem.GetAttribute('message')
            $body = $problem.InnerText
            $failures.Add([pscustomobject]@{
                    Suite    = $label
                    Name     = Get-TestDisplayName $className $case.GetAttribute('name')
                    Message  = if ($message) { $message.Trim() } else { ($body -split "`n" | Select-Object -First 1).Trim() }
                    Detail   = $body.TrimStart("`r", "`n").TrimEnd()
                    Location = Find-FailureLocation "$body`n$message" $className $report.DirectoryName
                })
        }
        elseif ($case.SelectSingleNode('skipped')) {
            $row.Skipped++
        }
        else {
            $row.Passed++
        }
    }
    $rows.Add($row)
}

$passed = 0
$failed = 0
$skipped = 0
$seconds = 0.0
foreach ($row in $rows) {
    $passed += $row.Passed
    $failed += $row.Failed
    $skipped += $row.Skipped
    $seconds += $row.Seconds
}

# Console digest — the same everywhere, so a local run and the CI log read alike.
if ($rows.Count -eq 0) {
    $message = "No JUnit reports matched: $($Path -join ', ')"
    Write-Warning "${Title}: $message"
    if ($onGitHub) {
        Write-Host "::warning title=$(ConvertTo-CommandProperty $Title)::$(ConvertTo-CommandData $message)"
    }
}
else {
    $color = if ($failed -gt 0) { 'Red' } else { 'Green' }
    Write-Host ("{0}: {1} passed, {2} failed, {3} skipped across {4} suites ({5})" -f $Title, (Format-Count $passed),
        (Format-Count $failed), (Format-Count $skipped), $rows.Count, (Format-Duration $seconds)) -ForegroundColor $color
    foreach ($failure in $failures) {
        $where = if ($failure.Location) { " [$($failure.Location.File)$(if ($failure.Location.Line) { ":$($failure.Location.Line)" })]" } else { '' }
        $firstLine = ($failure.Message -split "`n" | Select-Object -First 1)
        Write-Host "  FAILED $($failure.Name)$where — $firstLine" -ForegroundColor Red
    }
}

if (-not $onGitHub) {
    return
}

foreach ($failure in $failures | Select-Object -First $maxAnnotations) {
    $properties = @()
    if ($failure.Location) {
        $properties += "file=$(ConvertTo-CommandProperty $failure.Location.File)"
        if ($failure.Location.Line) {
            $properties += "line=$($failure.Location.Line)"
        }
    }
    $properties += "title=$(ConvertTo-CommandProperty "$($failure.Name) ($($failure.Suite))")"
    $text = Get-Truncated $failure.Message $maxFailureText
    Write-Host "::error $($properties -join ',')::$(ConvertTo-CommandData $text)"
}

if (-not $env:GITHUB_STEP_SUMMARY) {
    return
}

$summary = [System.Text.StringBuilder]::new()
if ($rows.Count -eq 0) {
    [void]$summary.AppendLine("## ⚠️ ${Title}: no test results found").AppendLine()
}
else {
    $headline = if ($failed -gt 0) {
        "❌ ${Title}: $(Format-Count $failed) failed, $(Format-Count $passed) passed"
    }
    else {
        "✅ ${Title}: $(Format-Count $passed) passed"
    }
    if ($skipped -gt 0) {
        $headline += ", $(Format-Count $skipped) skipped"
    }
    [void]$summary.AppendLine("## $headline").AppendLine()
    [void]$summary.AppendLine('| | Suite | Passed | Failed | Skipped | Test time |')
    [void]$summary.AppendLine('| :-: | :-- | --: | --: | --: | --: |')
    foreach ($row in $rows | Sort-Object Label) {
        $icon = if ($row.Failed -gt 0) { '❌' } else { '✅' }
        [void]$summary.AppendLine("| $icon | $(ConvertTo-TableCell $row.Label) | $(Format-Count $row.Passed) | $(Format-Count $row.Failed) | $(Format-Count $row.Skipped) | $(Format-Duration $row.Seconds) |")
    }
    [void]$summary.AppendLine("| | **Total ($($rows.Count) suites)** | **$(Format-Count $passed)** | **$(Format-Count $failed)** | **$(Format-Count $skipped)** | **$(Format-Duration $seconds)** |")
    [void]$summary.AppendLine()

    if ($failures.Count -gt 0) {
        [void]$summary.AppendLine('### Failed tests').AppendLine()
        foreach ($failure in $failures | Select-Object -First $maxDetailedFailures) {
            $where = if ($failure.Location) {
                " — <code>$(ConvertTo-HtmlText "$($failure.Location.File)$(if ($failure.Location.Line) { ":$($failure.Location.Line)" })")</code>"
            }
            else {
                ''
            }
            $text = $failure.Message
            if ($failure.Detail -and $failure.Detail -ne $failure.Message) {
                $text += "`n`n" + $failure.Detail
            }
            $text = Get-Truncated $text $maxFailureText
            $fence = Get-CodeFence $text
            [void]$summary.AppendLine("<details><summary>❌ <code>$(ConvertTo-HtmlText $failure.Name)</code> ($(ConvertTo-HtmlText $failure.Suite))$where</summary>").AppendLine()
            [void]$summary.AppendLine("${fence}text").AppendLine($text).AppendLine($fence).AppendLine()
            [void]$summary.AppendLine('</details>').AppendLine()
        }
        if ($failures.Count -gt $maxDetailedFailures) {
            [void]$summary.AppendLine("…and $(Format-Count ($failures.Count - $maxDetailedFailures)) more; the JUnit reports are in the run's test-results artifact.").AppendLine()
        }
    }
}

Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary.ToString() -Encoding utf8
