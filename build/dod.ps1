#!/usr/bin/env pwsh
#requires -version 7
<#
.SYNOPSIS
    The NCode.Identity Definition of Done — the single entrypoint CI and developers both run.

.DESCRIPTION
    Restores, builds (net10, warnings and vulnerability advisories as errors in CI), and tests the whole family
    with coverage. Runs identically locally and in CI so "it passes on my machine" means "it passes in the pipeline".

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER SkipTests
    Build only; skip the test + coverage stage.

.PARAMETER Fast
    Local inner-loop accelerator and the local pre-push gate (NOT a CI-parity gate): parallelizes project builds and
    test assemblies, and skips the non-correctness stages (coverage collection and NuGet packing). Compile
    warnings-as-errors and analyzers stay on, but NuGet vulnerability advisories (NU1901-1904) are warnings instead of
    errors. The plain ./build/dod.ps1 (no -Fast) is the CI-parity gate that CI runs.

.EXAMPLE
    ./build/dod.ps1

.EXAMPLE
    ./build/dod.ps1 -Fast
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Fast
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'NCode.Identity.slnx'

# Enforce the strict gate: warnings-as-errors and NuGet vulnerability advisories (NU1901-1904) fail the build.
# -Fast is a genuine local build: leaving CI unset keeps compile warnings-as-errors (unconditional in the props) but
# demotes vulnerability advisories to warnings, matching the local non-CI experience. The caller's CI value is restored
# on exit so a full run cannot leak CI mode into a later -Fast run in the same shell.
$originalCi = $env:CI
$env:CI = if ($Fast) { $null } else { 'true' }

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot
try {
    Invoke-Step 'tool-restore' { dotnet tool restore }
    Invoke-Step 'format (csharpier check)' { dotnet csharpier check . }

    # ADR index gate: the index in docs/adr/README.md is maintained by hand, and hand-maintenance demonstrably fails --
    # ten ADRs sat unindexed and a malformed delimiter row (which silently degrades the whole table to a paragraph)
    # survived dozens of commits before anyone noticed. An unindexed ADR is an ADR nobody finds, which defeats the
    # point of writing it (ADR-0008). Cheap and build-free, so it runs before the slow stages.
    Invoke-Step 'adr index' {
        $adrDir = Join-Path $repoRoot 'docs/adr'
        $indexPath = Join-Path $adrDir 'README.md'
        $indexLines = Get-Content $indexPath
        $problems = @()

        # A GitHub table renders only while every row has the same column count as its delimiter row. One row with the
        # wrong count collapses the entire table into a run-on paragraph, so this is a correctness check, not cosmetics.
        for ($i = 0; $i -lt $indexLines.Count; $i++) {
            if ($indexLines[$i].StartsWith('|')) {
                $columns = $indexLines[$i].TrimEnd().Split('|').Count - 2
                if ($columns -ne 3) {
                    $problems += "docs/adr/README.md($($i + 1)): table row has $columns columns, expected 3."
                }
            }
        }

        # 0000-template.md is the template, not a decision, so it is deliberately absent from the index.
        $onDisk = Get-ChildItem $adrDir -Filter '0*.md' |
            Where-Object { $_.Name -match '^(?!0000-)(\d{4})-' } |
            ForEach-Object { $_.Name }
        $indexed = $indexLines |
            Select-String -Pattern '^\| \[\d{4}\]\(\./([^)]+)\)' |
            ForEach-Object { $_.Matches[0].Groups[1].Value }

        foreach ($file in $onDisk | Where-Object { $_ -notin $indexed }) {
            $problems += "docs/adr/$file is not listed in docs/adr/README.md."
        }
        foreach ($entry in $indexed | Where-Object { $_ -notin $onDisk }) {
            $problems += "docs/adr/README.md links ./$entry, which does not exist."
        }

        if ($problems.Count -gt 0) {
            $problems | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            throw "adr index failed with $($problems.Count) problem(s)."
        }
        $global:LASTEXITCODE = 0
    }

    Invoke-Step 'restore' { dotnet restore $solution }
    # Build also PACKS: GeneratePackageOnBuild (Directory.Build.targets) emits each shippable .nupkg + .snupkg to
    # artifacts/packages as part of the build, so there is no separate pack step. -Fast builds projects in parallel
    # (-maxcpucount) and skips packing, which is a publishing artifact rather than a correctness gate.
    Invoke-Step 'build' {
        if ($Fast) {
            dotnet build $solution --configuration $Configuration --no-restore -maxcpucount -p:GeneratePackageOnBuild=false
        }
        else {
            dotnet build $solution --configuration $Configuration --no-restore
        }
    }

    # EF model-drift gate: fail if either host context has model changes no migration captures. The dev database
    # is Postgres, applied by the migration worker (ADR-0073), so migrations are authoritative — an un-migrated model
    # edit must fail pre-push and in CI, when the feature is added. Both contexts share the dev database (OpenID data +
    # the developer DataProtection key ring), so both are checked. Reuses the build output (--no-build); exit code 1 =
    # pending changes. The Database project carries the design-time factories, so it is both project and startup.
    Invoke-Step 'ef model drift' {
        dotnet ef migrations has-pending-model-changes `
            --no-build `
            --configuration $Configuration `
            --project src/NCode.Identity.OpenId.Host.Database/NCode.Identity.OpenId.Host.Database.csproj `
            --startup-project src/NCode.Identity.OpenId.Host.Database/NCode.Identity.OpenId.Host.Database.csproj `
            --context OpenIdDbContext
        if ($LASTEXITCODE -ne 0) {
            throw "ef model drift (OpenIdDbContext) failed with exit code $LASTEXITCODE."
        }
        dotnet ef migrations has-pending-model-changes `
            --no-build `
            --configuration $Configuration `
            --project src/NCode.Identity.OpenId.Host.Database/NCode.Identity.OpenId.Host.Database.csproj `
            --startup-project src/NCode.Identity.OpenId.Host.Database/NCode.Identity.OpenId.Host.Database.csproj `
            --context DeveloperDataProtectionDbContext
    }

    if (-not $SkipTests) {
        Invoke-Step 'test' {
            if ($Fast) {
                # Skip coverage collection and run the test assemblies in parallel across cores
                # (RunConfiguration.MaxCpuCount=0); xUnit already parallelizes collections within each assembly.
                dotnet test $solution `
                    --configuration $Configuration `
                    --no-build `
                    --logger:"junit;LogFilePath={assembly}-{framework}.junit.xml" `
                    -- RunConfiguration.MaxCpuCount=0
            }
            else {
                dotnet test $solution `
                    --configuration $Configuration `
                    --no-build `
                    --collect:"XPlat Code Coverage" `
                    --logger:"junit;LogFilePath={assembly}-{framework}.junit.xml"
            }
        }

        # -Fast skips coverage collection, so there is nothing to merge into a report.
        if (-not $Fast) {
            Invoke-Step 'coverage report' {
                # Merge every per-project/per-TFM cobertura file into one report + a text summary. The summary line
                # ("Line coverage: NN%") is echoed so a CI system can scrape the overall percentage, and the merged
                # Cobertura.xml drives per-line diff coverage views.
                $coverageDir = Join-Path $repoRoot 'artifacts/coverage'
                dotnet reportgenerator `
                    "-reports:$(Join-Path $repoRoot '**/coverage.cobertura.xml')" `
                    "-targetdir:$coverageDir" `
                    "-reporttypes:Cobertura;TextSummary"
                if ($LASTEXITCODE -ne 0) { return }

                # ReportGenerator writes absolute source paths; rewrite them relative to the repo root (forward slashes)
                # so coverage lines up against the diff in a code-review UI.
                $cobertura = Join-Path $coverageDir 'Cobertura.xml'
                $content = (Get-Content $cobertura -Raw).Replace($repoRoot + '\', '').Replace($repoRoot + '/', '')
                $content = [regex]::Replace($content, 'filename="([^"]*)"', { param($m) 'filename="' + ($m.Groups[1].Value -replace '\\', '/') + '"' })
                Set-Content -Path $cobertura -Value $content -NoNewline

                Get-Content (Join-Path $coverageDir 'Summary.txt') | Write-Host
            }
        }
    }

    Write-Host "`nDefinition of Done: PASSED" -ForegroundColor Green
}
finally {
    $env:CI = $originalCi
    Pop-Location
}
