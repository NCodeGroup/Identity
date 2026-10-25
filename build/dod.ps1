#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    The NCode.Identity Definition of Done — the single entrypoint CI and developers both run.

.DESCRIPTION
    Restores, builds (net10, warnings and vulnerability advisories as errors in CI), and tests the whole family
    with coverage. Runs identically locally and in CI so "it passes on my machine" means "it passes in the pipeline".

    The gate is the same everywhere; only its presentation adapts. Every run writes its test results to
    artifacts/test-results (cleared first, so a report never mixes in an earlier run) and prints a test digest and a
    stage/duration table. A full run also writes the merged coverage report to artifacts/coverage (index.html is the
    browsable report). On GitHub Actions each stage is a collapsible log group, a failure is annotated with the stage
    that broke, and the job summary shows the stage table, the test results, and the coverage summary (ADR-0143).

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER SkipTests
    Build only; skip the test + coverage stage.

.PARAMETER Fast
    Local inner-loop accelerator (NOT a CI-parity gate; the local pre-push gate is -Fast -Affected): parallelizes
    project builds and skips the non-correctness stages (coverage collection and NuGet packing). Test assemblies run in
    parallel in every mode, so -Fast does not change that. Compile warnings-as-errors and analyzers stay on, but NuGet
    vulnerability advisories (NU1901-1904) are warnings instead of errors. The plain ./build/dod.ps1 (no -Fast) is the
    full gate that the merge queue, main and the release lines run (ADR-0263).

.PARAMETER Filter
    A `dotnet test --filter` expression; runs only the matching tests (for example `FullyQualifiedName~NCode.Time`).

.PARAMETER Affected
    Runs only the test projects that build/affected-tests.ps1 selects for the changes since the merge base of HEAD and
    -Base: the test projects in the reverse ProjectReference closure of the changed projects, all of them when a global
    build input changed, and none when no project changed. The build stage still builds the whole solution, so compile
    errors anywhere are caught. Composes with -Fast and -Filter.

.PARAMETER Base
    The commit-ish whose merge base with HEAD -Affected measures the changes from. Defaults to origin/main.

.EXAMPLE
    ./build/dod.ps1

.EXAMPLE
    ./build/dod.ps1 -Fast

.EXAMPLE
    ./build/dod.ps1 -Fast -Filter 'FullyQualifiedName~TokenEndpoint'

.EXAMPLE
    ./build/dod.ps1 -Fast -Affected
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Fast,
    [string]$Filter,
    [switch]$Affected,
    [string]$Base = 'origin/main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'NCode.Identity.slnx'
$testResults = Join-Path $repoRoot 'artifacts/test-results'
$onGitHub = $env:GITHUB_ACTIONS -eq 'true'

# Enforce the strict gate: warnings-as-errors and NuGet vulnerability advisories (NU1901-1904) fail the build.
# -Fast is a genuine local build: leaving CI unset keeps compile warnings-as-errors (unconditional in the props) but
# demotes vulnerability advisories to warnings, matching the local non-CI experience. The caller's CI value is restored
# on exit so a full run cannot leak CI mode into a later -Fast run in the same shell.
$originalCi = $env:CI
$env:CI = if ($Fast) { $null } else { 'true' }

$stages = [System.Collections.Generic.List[object]]::new()
$passed = $false
$testsSkipped = $false

function Format-Duration([TimeSpan]$Elapsed) {
    if ($Elapsed.TotalSeconds -lt 60) {
        return $Elapsed.TotalSeconds.ToString('0.0', [System.Globalization.CultureInfo]::InvariantCulture) + ' s'
    }
    return '{0}m {1:00}s' -f [int][Math]::Floor($Elapsed.TotalMinutes), $Elapsed.Seconds
}

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    if ($onGitHub) {
        Write-Host "::group::$Name"
    }
    else {
        Write-Host "=== $Name ===" -ForegroundColor Cyan
    }
    $stage = [pscustomobject]@{ Name = $Name; Passed = $false; Elapsed = [TimeSpan]::Zero }
    $stages.Add($stage)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            throw "$Name failed with exit code $LASTEXITCODE."
        }
        $stage.Passed = $true
    }
    finally {
        $stage.Elapsed = $stopwatch.Elapsed
        if ($onGitHub) {
            Write-Host '::endgroup::'
        }
    }
}

# The stage table: printed locally, and on GitHub placed at the TOP of the job summary — above the test and coverage
# sections the stages appended while running — so the verdict and the stage that broke are the first thing a reader
# sees.
function Write-StageSummary {
    $total = [TimeSpan]::Zero
    foreach ($stage in $stages) {
        $total += $stage.Elapsed
    }
    $verdict = if ($passed) { 'PASSED' } else { 'FAILED' }
    $mode = if ($Fast) { 'fast gate' } else { 'full gate' }
    if ($Filter) {
        $mode += ', filtered tests'
    }
    if ($Affected) {
        $mode += ', affected tests'
    }

    Write-Host "`nStages ($mode, $Configuration):"
    foreach ($stage in $stages) {
        $color = if ($stage.Passed) { 'Green' } else { 'Red' }
        Write-Host ('  {0,-28} {1,-6} {2,10}' -f $stage.Name, $(if ($stage.Passed) { 'ok' } else { 'FAILED' }), (Format-Duration $stage.Elapsed)) -ForegroundColor $color
    }
    Write-Host ('  {0,-28} {1,-6} {2,10}' -f 'total', '', (Format-Duration $total))

    if (-not $onGitHub -or -not $env:GITHUB_STEP_SUMMARY) {
        return
    }
    $icon = if ($passed) { '✅' } else { '❌' }
    $failedStage = $stages | Where-Object { -not $_.Passed } | Select-Object -First 1
    $headline = if ($failedStage) { "$icon Definition of Done: $verdict at ``$($failedStage.Name)``" } else { "$icon Definition of Done: $verdict" }
    $lines = @("## $headline", '', "_$([char]::ToUpperInvariant($mode[0]) + $mode.Substring(1)), $Configuration._", '', '| | Stage | Duration |', '| :-: | :-- | --: |')
    foreach ($stage in $stages) {
        $lines += "| $(if ($stage.Passed) { '✅' } else { '❌' }) | $($stage.Name) | $(Format-Duration $stage.Elapsed) |"
    }
    $lines += "| | **Total** | **$(Format-Duration $total)** |"
    $lines += ''
    try {
        $existing = if (Test-Path -LiteralPath $env:GITHUB_STEP_SUMMARY) { Get-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Raw } else { '' }
        Set-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value (($lines -join "`n") + "`n" + $existing) -Encoding utf8 -NoNewline
    }
    catch {
        # Presentation must never decide the verdict.
        Write-Warning "Could not write the job summary: $($_.Exception.Message)"
    }
}

Push-Location $repoRoot
try {
    Invoke-Step 'agent customizations' {
        & (Join-Path $PSScriptRoot 'tests\agent-customizations.tests.ps1')
        $global:LASTEXITCODE = 0
    }
    Invoke-Step 'affected-tests selector' {
        & (Join-Path $PSScriptRoot 'tests\affected-tests.tests.ps1')
        $global:LASTEXITCODE = 0
    }
    Invoke-Step 'build script help' {
        & (Join-Path $PSScriptRoot 'tests\script-help.tests.ps1')
        $global:LASTEXITCODE = 0
    }

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
            # Results go to one directory that is cleared first: the test-project TestResults folders would otherwise
            # accumulate every past run, and a wildcard over them merges stale coverage into the report.
            if (Test-Path -LiteralPath $testResults) {
                Remove-Item -LiteralPath $testResults -Recurse -Force
            }
            $testTarget = $solution
            if ($Affected) {
                $selection = & (Join-Path $PSScriptRoot 'affected-tests.ps1') -Root $repoRoot -Base $Base
                foreach ($reason in $selection.Reasons) {
                    Write-Host $reason
                }
                if (-not $selection.All) {
                    if ($selection.Projects.Count -eq 0) {
                        Write-Host 'No test project is affected; skipping the tests.'
                        $script:testsSkipped = $true
                        $global:LASTEXITCODE = 0
                        return
                    }
                    # A solution filter narrows `dotnet test` to the selected projects; its solution path is relative
                    # to the filter itself, which lives in artifacts/.
                    $artifacts = Join-Path $repoRoot 'artifacts'
                    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
                    $testTarget = Join-Path $artifacts 'affected.slnf'
                    $solutionFilter = @{
                        solution = @{
                            path     = '../NCode.Identity.slnx'
                            projects = [string[]]@($selection.Projects | ForEach-Object { $_.Replace('/', '\') })
                        }
                    }
                    $json = ConvertTo-Json -InputObject $solutionFilter -Depth 4
                    [IO.File]::WriteAllText($testTarget, $json, [System.Text.UTF8Encoding]::new($false))
                }
            }
            $junitLogger = "junit;LogFilePath=$(Join-Path $testResults '{assembly}-{framework}.junit.xml')"
            $testArguments = @(
                $testTarget,
                '--configuration', $Configuration,
                '--no-build',
                '--results-directory', $testResults,
                "--logger:$junitLogger"
            )
            if (-not $Fast) {
                # Coverage is collected per test host, so it is unaffected by running the assemblies in parallel.
                # coverage.runsettings sets the coverage scope: hand-written code, without generated sources or EF
                # migrations (ADR-0185). -Fast skips coverage collection.
                $testArguments += '--collect:XPlat Code Coverage'
                $testArguments += '--settings', (Join-Path $PSScriptRoot 'coverage.runsettings')
            }
            if ($Filter) {
                $testArguments += '--filter', $Filter
            }
            # Test assemblies run in parallel across cores in every mode (RunConfiguration.MaxCpuCount=0); xUnit already
            # parallelizes collections within each assembly.
            $testArguments += '--', 'RunConfiguration.MaxCpuCount=0'
            dotnet test @testArguments

            # Report whatever ran — failures are exactly when the digest and annotations matter — then let the test
            # run's own exit code decide the stage.
            $testExitCode = $LASTEXITCODE
            & (Join-Path $PSScriptRoot 'test-report.ps1') -Path 'artifacts/test-results/*.junit.xml' -Title '.NET tests' -Root $repoRoot
            $global:LASTEXITCODE = $testExitCode
        }

        # -Fast skips coverage collection, and a skipped test stage collects none, so there is nothing to merge.
        if (-not $Fast -and -not $testsSkipped) {
            Invoke-Step 'coverage report' {
                # Merge this run's per-project cobertura files into one report (Cobertura + HTML + summaries). The echoed
                # "Line coverage: NN%" line lets a CI system scrape the overall percentage, and the merged,
                # repository-relative Cobertura.xml is what Codecov ingests for per-line diff coverage.
                & (Join-Path $PSScriptRoot 'coverage-report.ps1') `
                    -Path 'artifacts/test-results/*/coverage.cobertura.xml' `
                    -TargetDirectory 'artifacts/coverage' `
                    -Title '.NET coverage' `
                    -ArtifactName 'coverage' `
                    -Root $repoRoot
            }
        }
    }

    $passed = $true
    Write-StageSummary
    # Keep this the final line: it is the documented pass signal (AGENTS.md, the push protocol).
    Write-Host "`nDefinition of Done: PASSED" -ForegroundColor Green
}
catch {
    # Name the broken stage in the run's Annotations panel, so the cause is visible without opening the log.
    if ($onGitHub) {
        $message = $_.Exception.Message.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
        Write-Host "::error title=Definition of Done::$message"
    }
    if (-not $passed) {
        Write-StageSummary
    }
    throw
}
finally {
    $env:CI = $originalCi
    Pop-Location
}
