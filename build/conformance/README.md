# Conformance automation harness

Committed, runnable realization of backlog `T6` Phase 4 / `T1` Phase D: it wires the external **OpenID
Foundation Conformance Suite** (the certified oracle) behind the in-house probe-runner **pre-flight**, so one
command drives both against the Playground OP. Design and the manual-triage plan live in
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
| [`oidcc-basic.json`](oidcc-basic.json)               | Basic OP plan config, with the `browser` automation block and seeded clients. |
| [`expected-failures.json`](expected-failures.json)   | Baseline of documented, intentional failures (starts empty).                  |
| [`expected-skips.json`](expected-skips.json)         | Baseline of documented, intentional skips (starts empty).                     |

The config values (alias `ncode-basic`, clients `client` / `client2`, the `conformance-user` identity) match
the deterministic seed in
[`appsettings.Conformance.json`](../../src/NCode.Identity.OpenId.Playground/appsettings.Conformance.json)
(`AddConformanceSeed`, backlog `T4`). Secrets are the dev-only conformance credentials — this is a local test
harness, never production.

## Prerequisites

1. **Docker** (Compose v2) and **Python 3** on PATH; the **.NET SDK** and **pnpm** for the OP + pre-flight.
2. A hosts-file entry so the suite's external base URL resolves to loopback:
    ```
    127.0.0.1  localhost.emobix.co.uk
    ```
3. On Windows, MongoDB bind-mounts are unreliable; use the suite's named-volume compose snippet if needed
   (see [`docs/conformance-testing.md`](../../docs/conformance-testing.md) Appendix H).

## Run

```powershell
# Default: launch the OP, pre-flight, bring up the suite, run the Config OP profile, tear down.
./build/conformance/run-conformance.ps1

# Also run the browser-driven Basic OP profile (currently blocked — see below):
./build/conformance/run-conformance.ps1 -IncludeBrowserPlans

# Against an OP you already started (e.g. the Conformance launch profile in another terminal):
./build/conformance/run-conformance.ps1 -UseRunningOp

# Pre-flight only (launch OP + run the probe runner; no Docker/Python needed) — a fast local check:
./build/conformance/run-conformance.ps1 -PreflightOnly
```

**Default profile scope.** The default run gates on the **Config OP** profile, which passes today. The
browser-driven **Basic OP** profile is opt-in (`-IncludeBrowserPlans`) because it is **blocked**: the suite's
default `BrowserControl` is a no-JavaScript **HtmlUnitDriver**, but the OP-hosted `apps/auth` login is a
client-rendered React SPA, so the `username` field never renders and every interactive module interrupts.
Resolving that (server-rendered / no-JS login+consent, or a real JS browser) is tracked in
[`docs/conformance/external-suite.md`](../../docs/conformance/external-suite.md) and needs its own ADR.

The pre-flight step bootstraps the frontend pnpm workspace via
[`build/frontend-install.ps1`](../frontend-install.ps1) (idempotent; also self-heals a partially-installed
`node_modules`). Pass `-SkipFrontendInstall` to skip it when the workspace is already installed.

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
[`BACKLOG.md`](../../BACKLOG.md) item or ADR that justifies it. The entry shape (matched by test name / variant
/ config / block / condition) is in [`docs/conformance-testing.md`](../../docs/conformance-testing.md)
Appendix F — fill it from the `--verbose` template the runner prints for any unexpected failure.
