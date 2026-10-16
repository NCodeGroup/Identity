#requires -Version 7

<#
.SYNOPSIS
    Drives the external OpenID Foundation Conformance Suite against the NCode.Identity OpenID Provider,
    with the in-house probe runner as a fast pre-flight (backlog `T6` Phase 4 / `T1`).

.DESCRIPTION
    This is the committed, runnable realization of the full-automation plan in
    `docs/conformance-testing.md` (Phase D) and `docs/conformance/external-suite.md`. It:

      1. launches the Playground OP on the reachable HTTPS host the suite drives (`host.docker.internal:5001`,
         the `Conformance` launch profile — backlog `T3`);
      2. runs the in-house headless probe runner (`@ncode/app-conformance-runner`) as a fast pre-flight that
         exercises the confidential / server-initiated legs a browser cannot, failing early before the slower
         external suite is even started;
      3. brings up the external certified suite from prebuilt Docker images (no Java build);
      4. drives the Config OP + Basic OP plans headlessly via the suite's `run-test-plan.py`, gated on the
         committed `expected-failures.json` / `expected-skips.json` baselines;
      5. tears everything down and surfaces a single non-zero exit code on any unexpected failure.

    It is deliberately NOT part of `build/dod.ps1` — the suite needs Docker + Python 3 and is slow, so it runs
    as an opt-in / nightly gate, keeping the `dod.ps1` inner loop fast and hermetic.

.NOTES
    Prerequisites (see `docs/conformance-testing.md`):
      - Docker (Compose v2) and Python 3 on PATH; the .NET SDK and pnpm (for the pre-flight).
      - A hosts-file entry: `127.0.0.1  localhost.emobix.co.uk` (the suite's external base URL).
      - The seeded conformance clients / test user from `appsettings.Conformance.json` (backlog `T4`).

.EXAMPLE
    ./build/conformance/run-conformance.ps1

.EXAMPLE
    ./build/conformance/run-conformance.ps1 -SkipPreflight -Plans oidcc-basic-certification-test-plan -Configs oidcc-basic.json
#>
[CmdletBinding()]
param(
    # The local suite base URL (prebuilt-image default; resolves to 127.0.0.1 via the hosts file).
    [string] $SuiteBase = 'https://localhost.emobix.co.uk:8443',

    # The origin the suite reaches the OP at; its discovery `issuer` must byte-match this (backlog `T3`).
    [string] $IssuerUrl = 'https://host.docker.internal:5001',

    # Where the conformance-suite repo is cloned for its `scripts/` + `docker-compose-prebuilt.yml` (gitignored).
    [string] $SuiteRepo = "$PSScriptRoot/.conformance-suite",

    [string] $SuiteGitUrl = 'https://gitlab.com/openid/conformance-suite.git',

    [string] $ConfigDir = $PSScriptRoot,

    [string] $ExportDir = "$PSScriptRoot/.results",

    # Test plans and their matching config files, paired by index. Defaults to the Config OP profile; the browser-driven
    # Basic OP profile is opt-in via -IncludeBrowserPlans (it needs Docker-reachable browser automation and a running
    # OP; see docs/conformance/external-suite.md).
    [string[]] $Plans = @('oidcc-config-certification-test-plan'),
    [string[]] $Configs = @('oidcc-config.json'),

    # Append the browser-driven Basic OP certification plan (with its required variants). It registers a fresh client
    # per module via Dynamic Client Registration (no shared static `alias`), so long interactive prompt flows no longer
    # serialize into the suite's `alias conflict`. Requires anonymous registration on the OP
    # (`registration_endpoint_allow_anonymous=true`, set in appsettings.Conformance.json).
    [switch] $IncludeBrowserPlans,

    # Skip the in-house probe-runner pre-flight (step 2).
    [switch] $SkipPreflight,

    # Assume the OP is already running at $IssuerUrl; do not launch or stop it.
    [switch] $UseRunningOp,

    # Skip bootstrapping the frontend pnpm workspace before the pre-flight (assume it is already installed).
    [switch] $SkipFrontendInstall,

    # Launch the OP and run only the in-house pre-flight; do not start the external Docker suite (no Docker/Python
    # needed). A fast local check of the confidential/server-initiated legs without the full certified run.
    [switch] $PreflightOnly,

    [int] $ReadyTimeoutSeconds = 120,

    # Hard wall-clock ceiling for a single `run-test-plan.py` invocation. If the suite container drops out mid-plan the
    # suite script retries with backoff; this watchdog kills a plan that blows past the deadline so a dead suite fails
    # fast instead of hanging overnight (backlog `T8`). A healthy Basic/Config OP plan completes in a couple of minutes,
    # so the generous default never trips on a good run while still bounding a wedged one.
    [int] $PlanTimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$FrontendDir = Join-Path $RepoRoot 'src/frontend'
$PlaygroundProject = Join-Path $RepoRoot 'src/NCode.Identity.OpenId.Playground'
$ComposeFile = Join-Path $SuiteRepo 'docker-compose-prebuilt.yml'

function Write-Step([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-Command([string] $Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH. See docs/conformance-testing.md for prerequisites."
    }
}

function Wait-ForUrl([string] $Url, [string] $Label, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            # Dev certs on both ends; -SkipCertificateCheck tolerates the snake-oil / dev TLS.
            $null = Invoke-WebRequest -Uri $Url -SkipCertificateCheck -TimeoutSec 5 -UseBasicParsing
            Write-Host "    $Label is ready." -ForegroundColor Green
            return
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "$Label did not become ready within $TimeoutSeconds seconds ($Url)."
}

function Assert-IssuerMatches([string] $DiscoveryUrl, [string] $ExpectedIssuer) {
    $doc = Invoke-RestMethod -Uri $DiscoveryUrl -SkipCertificateCheck -TimeoutSec 10
    if ($doc.issuer -ne $ExpectedIssuer) {
        throw "Discovery issuer '$($doc.issuer)' does not byte-match the reached origin '$ExpectedIssuer'. " +
        'The Config OP profile fails on an issuer mismatch — see docs/conformance/reachable-host.md.'
    }
    Write-Host "    Discovery issuer byte-matches '$ExpectedIssuer'." -ForegroundColor Green
}

function Stop-ProcessTree([int] $ProcessId) {
    # 'dotnet run' launches the actual OP (Kestrel) as a CHILD process, so stopping only the wrapper leaves the real OP
    # orphaned and still bound to the port. Kill the whole tree so a later run cannot silently test a stale orphan.
    if ($IsWindows) {
        & taskkill.exe /PID $ProcessId /T /F 2>$null | Out-Null
    }
    else {
        & pkill -TERM -P $ProcessId 2>$null
        & kill -TERM $ProcessId 2>$null
    }
}

function Invoke-WithDeadline([string] $FilePath, [string[]] $Arguments, [int] $TimeoutSeconds, [string] $Label) {
    # Run an external process under a hard wall-clock deadline, streaming its output live to this console (no
    # redirection, so UseShellExecute=false inherits our stdout/stderr handles). ProcessStartInfo.ArgumentList adds each
    # argument verbatim, so paths with spaces or the plan's '[...]' variant brackets need no manual quoting. On overrun
    # the whole child tree is killed and a conventional timeout code (124) is returned so the caller fails fast rather
    # than blocking forever on a suite that went unreachable mid-plan (backlog `T8`).
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FilePath
    $psi.UseShellExecute = $false
    foreach ($arg in $Arguments) { $psi.ArgumentList.Add($arg) }
    $proc = [System.Diagnostics.Process]::Start($psi)
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        Write-Host "    $Label exceeded the ${TimeoutSeconds}s deadline; the suite is likely unreachable — killing it and failing fast." -ForegroundColor Red
        Stop-ProcessTree $proc.Id
        $null = $proc.WaitForExit(10000)
        return 124
    }
    return $proc.ExitCode
}

function Clear-OpPort([int] $Port) {
    # Free the OP port of any orphaned listener left by a previously aborted run, so the suite always exercises a
    # freshly built OP instead of stale server artifacts still holding the port (Get-NetTCPConnection is Windows-only;
    # elsewhere a fresh launch simply fails fast if the port is taken).
    if (-not $IsWindows) { return }
    $owners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $owners) {
        Write-Host "    Freeing OP port $Port held by orphaned process $procId from a prior run." -ForegroundColor Yellow
        Stop-ProcessTree $procId
    }
}

# The screenshot-review placeholders that oidcc-prompt-login / oidcc-max-age-1 park on (OIDCC-3.1.2.1) are filled
# deterministically, inline in the scripted-browser flow, by the per-module `override` blocks in oidcc-basic.json
# (the suite-native `update-image-placeholder-optional` action — mirrors the suite's own
# .gitlab-ci/local-provider-oidcc-conformance-config.json). No out-of-band upload watcher is needed.

$opProcess = $null
$suiteUp = $false
$failed = $false

# Location is session-global, not script-scoped: any Set-Location / Push-Location here (including nvs' auto-switch hook
# triggered by entering src/frontend) leaks into the caller's shell. Capture the entry location and restore it on exit.
$OriginalLocation = Get-Location

try {
    if (-not $PreflightOnly) {
        Test-Command 'docker'
        Test-Command 'python'
    }

    # --- 1) Launch the Playground OP on the reachable HTTPS origin. --------------------------------
    $discoveryUrl = "$IssuerUrl/.well-known/openid-configuration"
    if ($UseRunningOp) {
        Write-Step "Using an already-running OP at $IssuerUrl."
    }
    else {
        Write-Step "Launching the Playground OP ($IssuerUrl, Conformance launch profile)."
        Test-Command 'dotnet'
        # Clear any orphaned OP still holding the port so the fresh 'dotnet run' build is what actually serves the suite.
        Clear-OpPort ([uri] $IssuerUrl).Port
        $opProcess = Start-Process -FilePath 'dotnet' -PassThru -NoNewWindow -ArgumentList @(
            'run',
            '--project', $PlaygroundProject,
            '--launch-profile', 'Conformance'
        )
    }
    Wait-ForUrl -Url $discoveryUrl -Label 'OP discovery document' -TimeoutSeconds $ReadyTimeoutSeconds
    Assert-IssuerMatches -DiscoveryUrl $discoveryUrl -ExpectedIssuer $IssuerUrl

    # --- 2) In-house probe-runner pre-flight (fast, confidential/server-initiated legs). -----------
    if ($SkipPreflight) {
        Write-Step 'Skipping the in-house probe-runner pre-flight (-SkipPreflight).'
    }
    else {
        Write-Step 'Running the in-house probe-runner pre-flight.'
        Test-Command 'pnpm'
        if (-not $SkipFrontendInstall) {
            # The pnpm workspace must be installed for vite-node to resolve; the bootstrap is idempotent and also
            # self-heals a partially-installed node_modules (a present root but missing per-app links).
            Write-Host '    Ensuring the frontend workspace is installed.' -ForegroundColor Gray
            & (Join-Path $RepoRoot 'build/frontend-install.ps1')
            if ($LASTEXITCODE -ne 0) { throw 'Frontend workspace install failed; cannot run the pre-flight.' }
        }
        Push-Location $FrontendDir
        try {
            # Dev TLS on the OP; let Node's fetch accept it for this local pre-flight only.
            $env:NODE_TLS_REJECT_UNAUTHORIZED = '0'
            $env:CONFORMANCE_CLIENT_ID = 'client'
            $env:CONFORMANCE_CLIENT_SECRET = 'conformance-secret-client'
            pnpm --filter '@ncode/app-conformance-runner' start -- --issuer $IssuerUrl
            if ($LASTEXITCODE -ne 0) {
                throw "The in-house probe-runner pre-flight failed (exit $LASTEXITCODE); not starting the external suite."
            }
        }
        finally {
            Remove-Item Env:NODE_TLS_REJECT_UNAUTHORIZED, Env:CONFORMANCE_CLIENT_ID, Env:CONFORMANCE_CLIENT_SECRET -ErrorAction SilentlyContinue
            Pop-Location
        }
    }

    # --- 3) Bring up the external certified suite (prebuilt images; no Java build). -----------------
    if ($PreflightOnly) {
        Write-Step 'Pre-flight only (-PreflightOnly); skipping the external conformance suite.'
    }
    else {
        if (-not (Test-Path $ComposeFile)) {
            Write-Step "Cloning the OpenID Foundation Conformance Suite into $SuiteRepo."
            Test-Command 'git'
            git clone --depth 1 $SuiteGitUrl $SuiteRepo
            if ($LASTEXITCODE -ne 0) { throw 'git clone of the conformance suite failed.' }
        }

        Write-Step 'Starting the conformance suite (docker compose, prebuilt images).'
        docker compose -f $ComposeFile up -d
        if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }
        $suiteUp = $true
        Wait-ForUrl -Url "$SuiteBase/" -Label 'Conformance suite' -TimeoutSeconds $ReadyTimeoutSeconds

        # --- 4) Drive the plans headlessly, gated on the baselines. --------------------------------
        # The suite's run-test-plan.py has its own Python deps (httpx, pyparsing); install them into a dedicated
        # virtual environment so the host's global Python is never polluted, and drive the plans with its interpreter.
        $venvDir = Join-Path $SuiteRepo '.venv'
        $venvPython = Join-Path $venvDir ($IsWindows ? 'Scripts/python.exe' : 'bin/python')
        if (-not (Test-Path $venvPython)) {
            Write-Step 'Creating a Python virtual environment for the suite scripts.'
            python -m venv $venvDir
            if ($LASTEXITCODE -ne 0) { throw 'python -m venv failed.' }
        }
        & $venvPython -m pip install --quiet --disable-pip-version-check -r (Join-Path $SuiteRepo 'scripts/requirements.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Installing the suite scripts'' Python requirements failed.' }

        Write-Step 'Running the external conformance plans.'
        $plansToRun = @($Plans)
        $configsToRun = @($Configs)
        if ($IncludeBrowserPlans) {
            # The Basic OP modules require explicit variants (server_metadata / client_registration). `dynamic_client`
            # makes the suite register a fresh client per module, avoiding the shared-`alias` serialization conflict.
            $plansToRun += 'oidcc-basic-certification-test-plan[server_metadata=discovery][client_registration=dynamic_client]'
            $configsToRun += 'oidcc-basic.json'
        }
        if ($plansToRun.Count -ne $configsToRun.Count) {
            throw "Plans ($($plansToRun.Count)) and Configs ($($configsToRun.Count)) must have the same count."
        }
        New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null
        $env:CONFORMANCE_SERVER = "$SuiteBase/"
        # Dev mode makes the suite's API token optional and relaxes TLS verification — the local self-hosted suite
        # runs without authentication, so without this run-test-plan.py hard-fails reading CONFORMANCE_TOKEN.
        $env:CONFORMANCE_DEV_MODE = '1'
        $env:DISABLE_SSL_VERIFY = '1'
        # Pin the suite script's own retry bounds explicitly instead of inheriting whatever defaults the freshly-cloned
        # upstream happens to ship (backlog `T8`): cap each HTTP request's internal retry budget, the per-module restart
        # retries, and the consecutive-failure circuit breaker so the suite self-aborts quickly on a dead/flapping
        # server. The wrapper-level Invoke-WithDeadline watchdog below is the hard outer bound that does not depend on
        # any of these upstream knobs still existing.
        $env:CONFORMANCE_RETRY_TIMEOUT = '120'
        $env:CONFORMANCE_RESTART_RETRIES = '2'
        $env:CONFORMANCE_MAX_CONSECUTIVE_FAILURES = '3'

        # The suite's test-plan parser only accepts a bare config filename (its grammar excludes ':' and '\\', so a
        # Windows absolute path is mis-parsed to the drive letter); run from the config dir and pass the filename.
        $runTestPlan = Join-Path $SuiteRepo 'scripts/run-test-plan.py'
        $expectedFailures = Join-Path $ConfigDir 'expected-failures.json'
        $expectedSkips = Join-Path $ConfigDir 'expected-skips.json'
        $exportDirFull = (New-Item -ItemType Directory -Force -Path $ExportDir).FullName

        # run-test-plan.py loads the whole baseline file for every plan invocation and fails the run if any entry never
        # matches a module ("expected failure/skip not found"). The baselines are keyed by configuration-filename, so a
        # plan whose config has no entries (e.g. the Config OP plan) would be failed by the other plan's entries. Scope
        # each invocation to a temp baseline containing only the entries whose configuration-filename matches this plan.
        $scopedBaselineFiles = [System.Collections.Generic.List[string]]::new()
        function New-ScopedBaseline([string] $BaselinePath, [string] $ConfigFileName, [string] $Tag) {
            $entries = @(Get-Content $BaselinePath -Raw | ConvertFrom-Json)
            $scoped = @($entries | Where-Object { $ConfigFileName -like $_.'configuration-filename' })
            $configStem = [System.IO.Path]::GetFileNameWithoutExtension($ConfigFileName)
            $tmp = Join-Path ([System.IO.Path]::GetTempPath()) "conf-$Tag-$configStem.json"
            # Pipe (not -InputObject) into ConvertTo-Json: passing the array via -InputObject with -AsArray makes the
            # whole array a single object that -AsArray then wraps again, emitting a nested [[...]] that run-test-plan.py
            # chokes on ('list' object has no attribute 'get'). Piping flattens it; guard the empty case to a bare [].
            $json = $scoped | ConvertTo-Json -Depth 10 -AsArray
            if ([string]::IsNullOrWhiteSpace($json)) { $json = '[]' }
            Set-Content -Path $tmp -Value $json -Encoding utf8
            $scopedBaselineFiles.Add($tmp)
            return $tmp
        }

        Push-Location $ConfigDir
        try {
            for ($i = 0; $i -lt $plansToRun.Count; $i++) {
                Write-Host "    Plan: $($plansToRun[$i])  Config: $($configsToRun[$i])" -ForegroundColor Yellow
                $planFailures = New-ScopedBaseline $expectedFailures $configsToRun[$i] 'failures'
                $planSkips = New-ScopedBaseline $expectedSkips $configsToRun[$i] 'skips'
                # Run under a hard wall-clock deadline so a suite that goes unreachable mid-plan fails fast instead of
                # hanging the whole wrapper indefinitely (backlog `T8`).
                $exitCode = Invoke-WithDeadline -FilePath $venvPython -TimeoutSeconds $PlanTimeoutSeconds `
                    -Label "Plan '$($plansToRun[$i])'" -Arguments @(
                    $runTestPlan,
                    $plansToRun[$i], $configsToRun[$i],
                    '--expected-failures-file', $planFailures,
                    '--expected-skips-file', $planSkips,
                    '--export-dir', $exportDirFull, '--verbose'
                )
                if ($exitCode -ne 0) { $failed = $true }
            }
        }
        finally {
            Pop-Location
            foreach ($f in $scopedBaselineFiles) {
                Remove-Item $f -ErrorAction SilentlyContinue
            }
        }

        # --- 4b) Render a single HTML summary of the run (what passed / warned / was skipped / failed). ---
        # Best-effort: a reporting hiccup must never mask the primary pass/fail result.
        try {
            Write-Step 'Rendering the HTML conformance report.'
            $reportPath = & (Join-Path $PSScriptRoot 'New-ConformanceReport.ps1') -ResultsDir $exportDirFull
            Write-Host "    Open the report: $reportPath" -ForegroundColor Green
        }
        catch {
            Write-Host "    Could not render the HTML report: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}
finally {
    # --- 5) Teardown (best-effort; never masks the primary result). -------------------------------
    if ($suiteUp) {
        Write-Step 'Stopping the conformance suite.'
        docker compose -f $ComposeFile down 2>$null
    }
    if ($opProcess) {
        Write-Step 'Stopping the Playground OP.'
        # Tree-kill: 'dotnet run' spawns the OP as a child, so Stop-Process on the wrapper alone would orphan it.
        Stop-ProcessTree $opProcess.Id
    }
    Remove-Item Env:CONFORMANCE_SERVER, Env:CONFORMANCE_DEV_MODE, Env:DISABLE_SSL_VERIFY, Env:CONFORMANCE_RETRY_TIMEOUT, Env:CONFORMANCE_RESTART_RETRIES, Env:CONFORMANCE_MAX_CONSECUTIVE_FAILURES -ErrorAction SilentlyContinue
    Set-Location $OriginalLocation
}

if ($failed) {
    throw 'Conformance run FAILED — see the exported result zips and the log-detail links above.'
}
Write-Host ''
Write-Host 'Conformance run PASSED.' -ForegroundColor Green
