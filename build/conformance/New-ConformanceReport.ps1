#requires -Version 7

<#
.SYNOPSIS
    Renders a single self-contained HTML report from the OpenID Foundation Conformance Suite result zips,
    so you can see at a glance what was tested / passed / skipped / warned / failed without scanning the
    long console output of `run-conformance.ps1`.

.DESCRIPTION
    `run-test-plan.py --export-dir` writes one result zip per plan execution; each zip contains the per-module
    `test-log-*.json` files. This script extracts those logs, reads the overall per-module result and the
    non-success conditions (FAILURE / WARNING / REVIEW), groups them by plan, and emits a color-coded HTML
    report with summary cards and expandable per-module detail.

    By default it reports on the newest zip per plan found in the results directory (the latest run of each
    plan). `run-conformance.ps1` calls it automatically after a run; it can also be run standalone to
    regenerate the report from already-exported zips.

.EXAMPLE
    ./build/conformance/New-ConformanceReport.ps1

.EXAMPLE
    ./build/conformance/New-ConformanceReport.ps1 -Open
#>
[CmdletBinding()]
param(
    # Directory holding the exported result zips (run-test-plan.py --export-dir).
    [string] $ResultsDir = "$PSScriptRoot/.results",

    # Explicit set of result zips to report on. When empty, the newest zip per plan in -ResultsDir is used.
    [string[]] $ZipFiles = @(),

    # Where to write the HTML report.
    [string] $OutputPath = "$PSScriptRoot/.results/report.html",

    # Open the generated report in the default browser.
    [switch] $Open
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-ZipSet {
    if ($ZipFiles -and $ZipFiles.Count -gt 0) {
        return @($ZipFiles | ForEach-Object { (Resolve-Path $_).Path })
    }
    if (-not (Test-Path $ResultsDir)) {
        throw "Results directory not found: $ResultsDir. Run build/conformance/run-conformance.ps1 first."
    }
    $zips = @(Get-ChildItem -Path $ResultsDir -Filter '*.zip' -File)
    if ($zips.Count -eq 0) {
        throw "No result zips found in $ResultsDir. Run build/conformance/run-conformance.ps1 first."
    }
    # The filename is '<plan-name-with-variants>-<planId>-<DD-Mon-YYYY>.zip'; collapse to the plan key by
    # stripping the trailing '-<planId>-<date>' so repeated runs of the same plan keep only the newest zip.
    $byPlan = @{}
    foreach ($zip in $zips) {
        $key = $zip.BaseName -replace '-[A-Za-z0-9]{10,}-\d{1,2}-[A-Za-z]{3}-\d{4}$', ''
        if (-not $byPlan.ContainsKey($key) -or $zip.LastWriteTime -gt $byPlan[$key].LastWriteTime) {
            $byPlan[$key] = $zip
        }
    }
    return @($byPlan.Values | Sort-Object Name | ForEach-Object { $_.FullName })
}

function Get-ModulesFromZip([string] $ZipPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("conf-report-" + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    try {
        Expand-Archive -Path $ZipPath -DestinationPath $tmp -Force
        $logs = @(Get-ChildItem -Path $tmp -Filter 'test-log-*.json' -File -Recurse)
        foreach ($log in $logs) {
            $j = Get-Content $log.FullName -Raw | ConvertFrom-Json
            $info = $j.testInfo
            $variant = if ($info.variant) {
                (@($info.variant.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', ')
            }
            else { '' }
            $conditions = @(
                $j.results |
                    Where-Object { ($_.PSObject.Properties.Name -contains 'result') -and ($_.result -in 'FAILURE', 'WARNING', 'REVIEW') } |
                    ForEach-Object {
                        [pscustomobject]@{
                            Result       = [string]$_.result
                            Message      = if ($_.PSObject.Properties.Name -contains 'msg') { [string]$_.msg } else { '' }
                            Requirements = if ($_.PSObject.Properties.Name -contains 'requirements') { @($_.requirements) -join ', ' } else { '' }
                        }
                    }
            )
            $result = if ([string]::IsNullOrWhiteSpace($info.result)) { 'INCOMPLETE' } else { [string]$info.result }
            [pscustomobject]@{
                Name       = [string]$info.testName
                Variant    = $variant
                Result     = $result
                Conditions = $conditions
            }
        }
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-PlanLabel([string] $ZipPath) {
    $base = [System.IO.Path]::GetFileNameWithoutExtension($ZipPath)
    return ($base -replace '-[A-Za-z0-9]{10,}-\d{1,2}-[A-Za-z]{3}-\d{4}$', '')
}

$resultMeta = [ordered]@{
    PASSED      = @{ Label = 'Passed'; Class = 'passed' }
    WARNING     = @{ Label = 'Warning'; Class = 'warning' }
    REVIEW      = @{ Label = 'Review'; Class = 'review' }
    SKIPPED     = @{ Label = 'Skipped'; Class = 'skipped' }
    FAILED      = @{ Label = 'Failed'; Class = 'failed' }
    INTERRUPTED = @{ Label = 'Interrupted'; Class = 'failed' }
    INCOMPLETE  = @{ Label = 'Incomplete'; Class = 'unknown' }
    UNKNOWN     = @{ Label = 'Unknown'; Class = 'unknown' }
}

function Get-ResultClass([string] $Result) {
    if ($resultMeta.Contains($Result)) { return $resultMeta[$Result].Class }
    return 'unknown'
}

function ConvertTo-HtmlText([string] $Text) {
    if ($null -eq $Text) { return '' }
    return [System.Net.WebUtility]::HtmlEncode($Text)
}

$zipSet = Resolve-ZipSet
Write-Host "Reporting on $($zipSet.Count) result zip(s):" -ForegroundColor Cyan
$zipSet | ForEach-Object { Write-Host "    $(Split-Path $_ -Leaf)" -ForegroundColor Gray }

$plans = foreach ($zip in $zipSet) {
    [pscustomobject]@{
        Label   = Get-PlanLabel $zip
        Zip     = Split-Path $zip -Leaf
        Modules = @(Get-ModulesFromZip $zip | Sort-Object Name)
    }
}

$allModules = @($plans | ForEach-Object { $_.Modules })
$totals = [ordered]@{}
foreach ($key in $resultMeta.Keys) { $totals[$key] = 0 }
foreach ($m in $allModules) {
    $k = if ($resultMeta.Contains($m.Result)) { $m.Result } else { 'UNKNOWN' }
    $totals[$k]++
}
$totalCount = $allModules.Count

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<!DOCTYPE html>')
[void]$sb.AppendLine('<html lang="en"><head><meta charset="utf-8">')
[void]$sb.AppendLine('<meta name="viewport" content="width=device-width, initial-scale=1">')
[void]$sb.AppendLine('<title>OpenID Conformance Report</title>')
[void]$sb.AppendLine(@'
<style>
  :root {
    --bg: #0f1115; --panel: #171a21; --panel2: #1e222b; --text: #e6e8ec; --muted: #9aa0aa;
    --border: #2a2f3a; --passed: #2ecc71; --warning: #f1c40f; --review: #3498db;
    --skipped: #7f8c8d; --failed: #e74c3c; --unknown: #9b59b6;
  }
  * { box-sizing: border-box; }
  body { margin: 0; background: var(--bg); color: var(--text);
    font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif; line-height: 1.45; }
  .wrap { max-width: 1100px; margin: 0 auto; padding: 24px; }
  h1 { font-size: 22px; margin: 0 0 4px; }
  .meta { color: var(--muted); font-size: 13px; margin-bottom: 20px; }
  .cards { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 24px; }
  .card { background: var(--panel); border: 1px solid var(--border); border-radius: 10px;
    padding: 12px 16px; min-width: 110px; }
  .card .n { font-size: 26px; font-weight: 700; }
  .card .l { font-size: 12px; color: var(--muted); text-transform: uppercase; letter-spacing: .04em; }
  .card.total .n { color: var(--text); }
  .card.passed .n { color: var(--passed); } .card.warning .n { color: var(--warning); }
  .card.review .n { color: var(--review); } .card.skipped .n { color: var(--skipped); }
  .card.failed .n { color: var(--failed); } .card.unknown .n { color: var(--unknown); }
  .plan { background: var(--panel); border: 1px solid var(--border); border-radius: 10px;
    margin-bottom: 20px; overflow: hidden; }
  .plan > h2 { font-size: 15px; margin: 0; padding: 14px 16px; background: var(--panel2);
    border-bottom: 1px solid var(--border); font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
  .plan > h2 .zip { color: var(--muted); font-size: 12px; font-weight: 400; margin-left: 8px; }
  table { width: 100%; border-collapse: collapse; }
  th, td { text-align: left; padding: 10px 16px; border-bottom: 1px solid var(--border); vertical-align: top; }
  th { font-size: 11px; text-transform: uppercase; letter-spacing: .04em; color: var(--muted); }
  tr.mod { cursor: pointer; }
  tr.mod:hover { background: var(--panel2); }
  td.name { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; }
  td.variant { color: var(--muted); font-size: 12px; }
  .badge { display: inline-block; padding: 2px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; }
  .badge.passed { background: rgba(46,204,113,.15); color: var(--passed); }
  .badge.warning { background: rgba(241,196,15,.15); color: var(--warning); }
  .badge.review { background: rgba(52,152,219,.15); color: var(--review); }
  .badge.skipped { background: rgba(127,140,141,.2); color: #bdc3c7; }
  .badge.failed { background: rgba(231,76,60,.15); color: var(--failed); }
  .badge.unknown { background: rgba(155,89,182,.15); color: var(--unknown); }
  tr.detail { display: none; }
  tr.detail.open { display: table-row; }
  tr.detail td { background: var(--panel2); }
  .cond { border-left: 3px solid var(--border); padding: 4px 0 4px 12px; margin: 6px 0; }
  .cond.FAILURE { border-color: var(--failed); }
  .cond.WARNING { border-color: var(--warning); }
  .cond.REVIEW { border-color: var(--review); }
  .cond .r { font-size: 11px; font-weight: 700; letter-spacing: .04em; }
  .cond.FAILURE .r { color: var(--failed); } .cond.WARNING .r { color: var(--warning); }
  .cond.REVIEW .r { color: var(--review); }
  .cond .m { font-size: 13px; }
  .cond .req { font-size: 11px; color: var(--muted); font-family: ui-monospace, monospace; }
  .nocond { color: var(--muted); font-size: 13px; }
  .hint { color: var(--muted); font-size: 12px; }
</style>
'@)
[void]$sb.AppendLine('</head><body><div class="wrap">')
[void]$sb.AppendLine('<h1>OpenID Conformance Report</h1>')
$generated = ConvertTo-HtmlText ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
[void]$sb.AppendLine("<div class=""meta"">Generated $generated &middot; $totalCount module(s) across $($plans.Count) plan(s) &middot; <span class=""hint"">click a row for condition detail</span></div>")

[void]$sb.AppendLine('<div class="cards">')
[void]$sb.AppendLine("<div class=""card total""><div class=""n"">$totalCount</div><div class=""l"">Total</div></div>")
foreach ($key in $resultMeta.Keys) {
    if ($totals[$key] -eq 0) { continue }
    $meta = $resultMeta[$key]
    [void]$sb.AppendLine("<div class=""card $($meta.Class)""><div class=""n"">$($totals[$key])</div><div class=""l"">$($meta.Label)</div></div>")
}
[void]$sb.AppendLine('</div>')

$rowId = 0
foreach ($plan in $plans) {
    $label = ConvertTo-HtmlText $plan.Label
    $zipName = ConvertTo-HtmlText $plan.Zip
    [void]$sb.AppendLine('<div class="plan">')
    [void]$sb.AppendLine("<h2>$label<span class=""zip"">$zipName</span></h2>")
    [void]$sb.AppendLine('<table><thead><tr><th>Module</th><th>Variant</th><th>Result</th></tr></thead><tbody>')
    foreach ($m in $plan.Modules) {
        $rowId++
        $cls = Get-ResultClass $m.Result
        $name = ConvertTo-HtmlText $m.Name
        $variant = ConvertTo-HtmlText $m.Variant
        $resLabel = ConvertTo-HtmlText $m.Result
        [void]$sb.AppendLine("<tr class=""mod"" onclick=""document.getElementById('d$rowId').classList.toggle('open')"">")
        [void]$sb.AppendLine("<td class=""name"">$name</td><td class=""variant"">$variant</td><td><span class=""badge $cls"">$resLabel</span></td></tr>")
        [void]$sb.AppendLine("<tr class=""detail"" id=""d$rowId""><td colspan=""3"">")
        if ($m.Conditions.Count -eq 0) {
            [void]$sb.AppendLine('<div class="nocond">No failing, warning, or review conditions.</div>')
        }
        else {
            foreach ($c in $m.Conditions) {
                $cr = ConvertTo-HtmlText $c.Result
                $cm = ConvertTo-HtmlText $c.Message
                [void]$sb.AppendLine("<div class=""cond $cr""><span class=""r"">$cr</span> <span class=""m"">$cm</span>")
                if ($c.Requirements) {
                    $req = ConvertTo-HtmlText $c.Requirements
                    [void]$sb.AppendLine("<div class=""req"">$req</div>")
                }
                [void]$sb.AppendLine('</div>')
            }
        }
        [void]$sb.AppendLine('</td></tr>')
    }
    [void]$sb.AppendLine('</tbody></table></div>')
}

[void]$sb.AppendLine('</div></body></html>')

$outDir = Split-Path $OutputPath -Parent
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
Set-Content -Path $OutputPath -Value $sb.ToString() -Encoding utf8
$resolved = (Resolve-Path $OutputPath).Path
Write-Host ''
Write-Host "Conformance report written to $resolved" -ForegroundColor Green

if ($Open) {
    if ($IsWindows) { Start-Process $resolved }
    elseif ($IsMacOS) { & open $resolved }
    else { & xdg-open $resolved }
}

return $resolved
