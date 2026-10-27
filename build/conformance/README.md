# Conformance automation harness

Committed, runnable realization of issue [#129](https://github.com/NCodeGroup/Identity/issues/129) Phase 4 / [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase D: it wires the external **OpenID
Foundation Conformance Suite** (the certified oracle) behind the in-house probe-runner **pre-flight**, so one
command drives both against the OpenID host. Design and the manual-triage plan live in
[`docs/conformance-testing.md`](../../docs/conformance-testing.md) and
[`docs/conformance/external-suite.md`](../../docs/conformance/external-suite.md).

This harness is **not** part of [`build/dod.ps1`](../dod.ps1): the external suite needs Docker + Python 3 and
is slow, so it runs as an opt-in / nightly gate while the `dod.ps1` inner loop stays fast and hermetic.

## Contents

| File                                                 | Role                                                                          |
| ---------------------------------------------------- | ----------------------------------------------------------------------------- |
| [`run-conformance.ps1`](run-conformance.ps1)         | Orchestration: launch OP → pre-flight → suite up → run plans → report → teardown. |
| [`New-ConformanceReport.ps1`](New-ConformanceReport.ps1) | Renders a self-contained HTML report (pass / warn / skip / fail) from the result zips. |
| [`oidcc-config.json`](oidcc-config.json)             | Config OP plan config (discovery-metadata only).                              |
| [`oidcc-basic.json`](oidcc-basic.json)               | Basic OP plan config, with the `browser` automation block (clients registered per module via DCR). |
| [`expected-failures.json`](expected-failures.json)   | Baseline of documented, intentional failures (each entry explains its deviation). |
| [`expected-skips.json`](expected-skips.json)         | Baseline of documented, intentional skips (each entry explains its skip).     |

The `conformance-user` login identity in the configs and the `client` credentials the pre-flight uses match the
deterministic seed in
[`appsettings.Conformance.json`](../../src/NCode.Identity.OpenId.Host/appsettings.Conformance.json)
(`AddConformanceSeed`, issue [#127](https://github.com/NCodeGroup/Identity/issues/127)); the Basic OP plan registers its own client per module via Dynamic Client
Registration. Secrets are the dev-only conformance credentials — this is a local test harness, never production.

## Prerequisites

1. **Docker** (Compose v2) running; the **.NET SDK** pinned in [`global.json`](../../global.json).
2. A real **Python 3** on PATH. The Windows Microsoft Store `python` alias is on PATH but fails when run, so it
   does not count.
3. **[NVS](https://github.com/jasongin/nvs)** for the pre-flight;
   [`build/frontend-install.ps1`](../frontend-install.ps1) adds the Node major pinned in
   [`src/frontend/.node-version`](../../src/frontend/.node-version), puts it on PATH, and installs the pinned pnpm.
4. Hosts-file entries so both origins resolve to loopback from the host:
    ```
    127.0.0.1  localhost.emobix.co.uk
    127.0.0.1  host.docker.internal
    ```
    `localhost.emobix.co.uk` is the suite's external base URL; `host.docker.internal` is the OP issuer, which the
    harness's readiness probe and the pre-flight reach from the host. Docker Desktop usually writes
    `host.docker.internal` itself, but a fresh install may not. Containers resolve it through Docker's internal
    DNS either way, so the loopback entry only affects host-side callers. The script fails fast if either name
    does not resolve.
5. On Windows, MongoDB bind-mounts are unreliable; use the suite's named-volume compose snippet if needed
   (see [`docs/conformance-testing.md`](../../docs/conformance-testing.md) Appendix H).

### One-time Windows machine setup

```powershell
# Python 3 via the Python install manager: python.org's current Windows installer (the per-version
# full installer is deprecated since 3.14). It provides the global `python` / `py` commands.
winget install 9NQ7512CXL7T -e --accept-package-agreements --disable-interactivity
py install default
python --version   # new terminal; must print "Python 3.x", not the Store "Python was not found" stub

# NVS, per-user (no admin): clone into %LOCALAPPDATA%\nvs and add it to the user PATH. The harness's first run adds
# the pinned Node major through it (build/frontend-install.ps1).
$env:NVS_HOME = "$env:LOCALAPPDATA\nvs"
git clone https://github.com/jasongin/nvs "$env:NVS_HOME"
. "$env:NVS_HOME\nvs.ps1" install

# Hosts entries — run from an ELEVATED PowerShell; appends only the missing ones.
$hostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
$missing = 'localhost.emobix.co.uk', 'host.docker.internal' | Where-Object {
    -not (Select-String -Path $hostsFile -Pattern "^\s*127\.0\.0\.1\s+$([regex]::Escape($_))\b" -Quiet) }
if ($missing) { Add-Content -Path $hostsFile -Value (@('') + ($missing | ForEach-Object { "127.0.0.1  $_" })) }
```

To have NVS switch to the pinned Node automatically on `cd` (optional; the harness does not need it), see the
shell-profile snippets in [`src/frontend/README.md`](../../src/frontend/README.md).

## Run

```powershell
# Default: launch the OP, pre-flight, bring up the suite, run the Config OP profile, tear down.
./build/conformance/run-conformance.ps1

# Also run the browser-driven Basic OP profile:
./build/conformance/run-conformance.ps1 -IncludeBrowserPlans

# Against an OP you already started (e.g. the Conformance launch profile in another terminal):
./build/conformance/run-conformance.ps1 -UseRunningOp

# Pre-flight only (launch OP + run the probe runner; no Docker/Python needed) — a fast local check:
./build/conformance/run-conformance.ps1 -PreflightOnly

# The FAPI 1.0 Advanced Final plans instead of the default ones:
./build/conformance/run-conformance.ps1 -Fapi1Advanced
```

**Default profile scope.** The default run gates on the **Config OP** profile only. The browser-driven **Basic OP**
profile is opt-in (`-IncludeBrowserPlans`) because it is slower: the suite's headless, no-JavaScript
**HtmlUnitDriver** drives the OP's interactive login and consent through the server-rendered no-JS form, under Dynamic
Client Registration. It runs green; status and triage history are in
[`docs/conformance/external-suite.md`](../../docs/conformance/external-suite.md).

**FAPI 1.0 Advanced.** `-Fapi1Advanced` runs `fapi1-advanced-final-test-plan` four times, with `private_key_jwt` and
`mtls` client authentication and the `plain_response` and `jarm` response modes, always with pushed authorization
requests ([ADR-0202](../../docs/adr/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)). The
`mtls` variant uses `self_signed_tls_client_auth`. Each of the four `fapi1-*` clients in
`appsettings.Conformance.json` gets a P-256 key and a self-signed certificate for it. They are generated on first use
into `./.fapi1-keys/` (gitignored, never committed) and reused, so the seeded clients keep matching a persistent
database; delete the folder and purge the database together. The script writes each client's public key set to the
environment variable `ConformanceSeed__Clients__<client_id>__Jwks`, which the OP inherits and which seeds the client.
It renders the suite configuration for each method, with the private keys and certificates, from the one
`fapi1-advanced.template.json`, filling in the issuer from `-IssuerUrl`, and writes `fapi1-advanced-<method>.json`
beside it (gitignored, deleted when the run ends). FAPI tokens are
certificate-bound, so the OP is launched requesting client certificates and advertising certificate-bound access
tokens and the `code id_token` response type; that is why the switch needs a fresh OP (not `-UseRunningOp`).
The pre-flight still runs first, as in the default run: advertised certificate binding requires a certificate only
from a client whose security profile requires binding (issue
[#233](https://github.com/NCodeGroup/Identity/issues/233)), and the pre-flight's `client` selects none.
`-Fapi1Advanced -PreflightOnly` runs only that pre-flight against the FAPI-configured OP, without Docker or Python.

The pre-flight step bootstraps the frontend pnpm workspace via
[`build/frontend-install.ps1`](../frontend-install.ps1) (idempotent; also self-heals a partially-installed
`node_modules`). Pass `-SkipFrontendInstall` to skip it when the workspace is already installed.

**Fail-fast on an unreachable suite.** Each `run-test-plan.py` plan invocation runs under a hard wall-clock
watchdog (`-PlanTimeoutSeconds`, default `1800`): if the suite container drops out mid-plan the suite script
would otherwise retry with backoff and hang the wrapper indefinitely, so a plan that blows past the deadline is
killed (process tree) and the run fails fast. The harness also pins the suite script's own retry bounds
(`CONFORMANCE_RETRY_TIMEOUT`, `CONFORMANCE_RESTART_RETRIES`, `CONFORMANCE_MAX_CONSECUTIVE_FAILURES`) so the suite
self-aborts quickly on a dead/flapping server rather than relying on whatever defaults the freshly-cloned
upstream ships (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).

On first run the script shallow-clones the suite repo into `./.conformance-suite/` (gitignored) for its
`scripts/run-test-plan.py` and `docker-compose-prebuilt.yml`, and creates a Python venv there for the suite's
script dependencies. Per-plan result zips are written to `./.results/` (gitignored); diff successive runs with
the suite's `compare-results.py`.

## Report

Instead of scanning the suite's long console output, every run renders a single self-contained HTML report at
`./.results/report.html` (gitignored) summarizing — per plan and per module — what **passed / warned / was
skipped / failed** (plus `incomplete` for interrupted modules). Summary cards give the run-wide counts; clicking
a module row expands its failing / warning / review conditions with the OIDCC requirement references.

Regenerate the report from already-exported zips without re-running the suite, and open it in the browser:

```powershell
./build/conformance/New-ConformanceReport.ps1 -Open
```

By default it reports on the newest zip per plan in `./.results/`.

The script exits non-zero if the pre-flight fails **or** any external plan reports an unexpected failure (one
not codified in the baselines). A baseline entry that stops firing also fails the run, so the baselines cannot
silently rot.

## Baselines

Start empty (`[]`); add an entry only for a **documented, intentional** deviation, with a `comment` linking the
issue or ADR that justifies it. The entry shape (matched by test name / variant
/ config / block / condition) is in [`docs/conformance-testing.md`](../../docs/conformance-testing.md)
Appendix F — fill it from the `--verbose` template the runner prints for any unexpected failure.
