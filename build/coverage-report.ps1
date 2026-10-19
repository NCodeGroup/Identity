#!/usr/bin/env pwsh
#requires -version 7
<#
.SYNOPSIS
    Merges Cobertura coverage into one report: Cobertura + browsable HTML + text, plus a job summary on GitHub.

.DESCRIPTION
    Runs ReportGenerator (a pinned local tool, .config/dotnet-tools.json) over the given Cobertura files and writes to
    -TargetDirectory:

      - Cobertura.xml       the merged report, with every file path repository-relative so it lines up against the
                            diff in a code-review UI (Codecov);
      - index.html          the browsable HTML report;
      - Summary.txt         the text summary, whose header block (with the "Line coverage: NN%" line) is echoed;
      - SummaryGithub.md    the markdown summary, appended to the step's job summary on GitHub Actions.

    The target directory is cleared first, so a report never mixes in a previous run's pages.

.PARAMETER Path
    One or more Cobertura report paths. Relative paths are resolved against -Root; PowerShell wildcards are allowed in
    any segment (for example 'artifacts/test-results/*/coverage.cobertura.xml').

.PARAMETER TargetDirectory
    Where the merged report is written. Relative paths are resolved against -Root.

.PARAMETER Title
    The report title and job-summary heading, for example '.NET coverage'.

.PARAMETER ArtifactName
    The name of the workflow artifact the HTML report is uploaded as; when given, the job summary points readers to it.

.PARAMETER PerPackageReports
    The inputs are per-workspace-package JavaScript reports (Vitest/istanbul). Istanbul names every report's root
    package 'main' and every class by its bare file name, so merging them as-is would fold same-named files from
    different packages (every 'src/index.ts') into one class. With this switch each report is first rewritten so its
    packages carry the owning workspace package's name (package.json) and its classes carry their package-relative
    file path.

.PARAMETER Root
    The repository root. Defaults to the parent of this script's directory.

.EXAMPLE
    ./build/coverage-report.ps1 -Path 'artifacts/test-results/*/coverage.cobertura.xml' -TargetDirectory artifacts/coverage -Title '.NET coverage'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$Path,
    [Parameter(Mandatory)]
    [string]$TargetDirectory,
    [string]$Title = 'Coverage',
    [string]$ArtifactName,
    [switch]$PerPackageReports,
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# GitHub rejects a step summary over 1 MiB; past this size only the overall summary table is kept.
$maxSummaryBytes = 900KB
$Root = [System.IO.Path]::GetFullPath($Root)
$rootForward = $Root.Replace('\', '/').TrimEnd('/')

function Resolve-UnderRoot([string]$Value) {
    if ([System.IO.Path]::IsPathRooted($Value)) {
        return [System.IO.Path]::GetFullPath($Value)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $Root $Value))
}

# Repository-relative, forward-slashed. Deterministic CI builds map the repository root to '/_/' (SourceLink); coverlet
# maps most paths back, but generated files keep the mapped form.
function ConvertTo-RepositoryPath([string]$Value) {
    $value = $Value.Replace('\', '/')
    if ($value.StartsWith($rootForward + '/', [StringComparison]::OrdinalIgnoreCase)) {
        return $value.Substring($rootForward.Length + 1)
    }
    if ($value -match '^/_\d*/(?<rest>.*)$') {
        return $Matches.rest
    }
    return $value
}

function Save-Xml([xml]$Document, [string]$FilePath) {
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    # The Cobertura DTD reference is informational and no consumer validates it; re-serializing it would also append an
    # empty internal subset ("[]").
    if ($Document.DocumentType) {
        [void]$Document.RemoveChild($Document.DocumentType)
    }
    $writer = [System.Xml.XmlWriter]::Create($FilePath, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Read-Xml([string]$FilePath) {
    $document = [System.Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    $document.Load($FilePath)
    return $document
}

function Get-PackageName([string]$Directory) {
    $packageJson = Join-Path $Directory 'package.json'
    if (Test-Path -LiteralPath $packageJson -PathType Leaf) {
        $package = Get-Content -LiteralPath $packageJson -Raw | ConvertFrom-Json
        if ($package.PSObject.Properties.Name -contains 'name' -and $package.name) {
            return [string]$package.name
        }
    }
    return ConvertTo-RepositoryPath $Directory
}

# Rewrites one istanbul report so its packages and classes cannot collide with another package's when merged. A class
# is named for its package-relative path with '.' separators — ReportGenerator reads '/' in a class name as a .NET
# nested-class separator and would fold every file into one class named for its first directory — and its file name
# becomes absolute, so ReportGenerator resolves it without relying on the report's <source>.
function ConvertTo-QualifiedReport([string]$Report, [string]$Destination) {
    $document = Read-Xml $Report
    $sourceNode = $document.SelectSingleNode('/coverage/sources/source')
    $sourceRoot = if ($sourceNode -and $sourceNode.InnerText) { $sourceNode.InnerText } else { Split-Path -Parent (Split-Path -Parent $Report) }
    $sourceRoot = [System.IO.Path]::GetFullPath($sourceRoot)
    $packageName = Get-PackageName $sourceRoot

    foreach ($package in $document.SelectNodes('//packages/package')) {
        $package.SetAttribute('name', $packageName)
    }
    foreach ($class in $document.SelectNodes('//classes/class')) {
        $relative = $class.GetAttribute('filename').Replace('\', '/')
        $class.SetAttribute('name', $relative.Replace('/', '.'))
        $class.SetAttribute('filename', [System.IO.Path]::GetFullPath((Join-Path $sourceRoot $relative)).Replace('\', '/'))
    }
    Save-Xml $document $Destination
}

$reports = @(
    @(
        foreach ($pattern in $Path) {
            Get-ChildItem -Path (Resolve-UnderRoot $pattern) -File -ErrorAction SilentlyContinue
        }
    ) | Sort-Object FullName -Unique
)

if ($reports.Count -eq 0) {
    Write-Host "No Cobertura reports matched: $($Path -join ', ')" -ForegroundColor Red
    exit 1
}

$target = Resolve-UnderRoot $TargetDirectory
if (Test-Path -LiteralPath $target) {
    Remove-Item -LiteralPath $target -Recurse -Force
}
New-Item -ItemType Directory -Path $target -Force | Out-Null

$staging = $null
Push-Location $Root
try {
    $inputs = $reports.FullName
    if ($PerPackageReports) {
        $staging = Join-Path ([System.IO.Path]::GetTempPath()) "coverage-report-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $staging | Out-Null
        $inputs = for ($i = 0; $i -lt $reports.Count; $i++) {
            $destination = Join-Path $staging "$i.cobertura.xml"
            ConvertTo-QualifiedReport $reports[$i].FullName $destination
            $destination
        }
    }

    dotnet reportgenerator `
        "-reports:$($inputs -join ';')" `
        "-targetdir:$target" `
        '-reporttypes:Cobertura;Html;MarkdownSummaryGithub;TextSummary' `
        "-title:$Title" `
        '-verbosity:Warning'
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
    if ($staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Every file path repository-relative, and one '.' source, so a consumer that joins <source> with a file name (Codecov)
# lands on the repository file rather than a path that only existed on the build machine.
$coberturaPath = Join-Path $target 'Cobertura.xml'
$cobertura = Read-Xml $coberturaPath
foreach ($class in $cobertura.SelectNodes('//classes/class')) {
    $class.SetAttribute('filename', (ConvertTo-RepositoryPath $class.GetAttribute('filename')))
}
$sources = $cobertura.SelectSingleNode('/coverage/sources')
if ($sources) {
    $sources.RemoveAll()
    $source = $cobertura.CreateElement('source')
    $source.InnerText = '.'
    [void]$sources.AppendChild($source)
}
Save-Xml $cobertura $coberturaPath

# Echo only the header block of the text summary (overall figures, including "Line coverage: NN%"); the per-class
# breakdown is in the HTML report.
$summaryText = @(Get-Content -LiteralPath (Join-Path $target 'Summary.txt'))
$headerEnd = [Array]::IndexOf($summaryText, '')
$header = if ($headerEnd -gt 0) { $summaryText[0..($headerEnd - 1)] } else { $summaryText }
$header | Write-Host
Write-Host "Full report: $(Join-Path $target 'index.html')"

if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:GITHUB_STEP_SUMMARY) {
    return
}

# ReportGenerator's markdown opens with a document-level "# Summary"; nest it under this report's own heading, and drop
# the rows its free edition replaces with a sponsorship link.
$markdown = Get-Content -LiteralPath (Join-Path $target 'SummaryGithub.md') -Raw
$markdown = [regex]::Replace($markdown, '(?m)^# Summary\b.*\r?\n', '')
$markdown = [regex]::Replace($markdown, '(?m)^## Coverage\s*$', '### Coverage by assembly')
$markdown = [regex]::Replace($markdown, '(?m)^.*reportgenerator\.io/pro.*\r?\n', '')
if ([System.Text.Encoding]::UTF8.GetByteCount($markdown) -gt $maxSummaryBytes) {
    $breakdown = $markdown.IndexOf('### Coverage by assembly')
    $overall = if ($breakdown -gt 0) { $markdown.Substring(0, $breakdown) } else { '' }
    $markdown = $overall + "_The per-class breakdown is too large for a job summary; see the HTML report._`n"
}

$note = if ($ArtifactName) { "The browsable HTML report is in the run's ``$ArtifactName`` artifact.`n`n" } else { '' }
Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "## 📊 $Title`n`n$note$($markdown.Trim())`n" -Encoding utf8
