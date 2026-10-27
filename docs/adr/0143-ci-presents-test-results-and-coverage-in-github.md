# 143. CI presents test results and coverage in GitHub, through job summaries, annotations, and Codecov

- **Status:** Accepted (coverage scope set by
  [ADR-0185](0185-coverage-measures-hand-written-code-and-counts-a-line-that-runs-as-covered.md))
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Both CI tiers already produce rich results: the .NET gate ([`build/dod.ps1`](../../build/dod.ps1)) writes a JUnit report
per test assembly and a merged Cobertura coverage report, and the frontend workspace runs Vitest across every package.
Uploading those files only as zip artifacts leaves a run page that says nothing beyond "Success": no test counts, no
coverage, no failed test located in the code, no indication of which stage of the gate broke.

GitHub has no built-in JUnit or Cobertura viewer, but it does render two things natively, with no write permission:

- the **job summary** — markdown a step writes to `$GITHUB_STEP_SUMMARY`, shown on the run page and on a pull request's
  checks; and
- **annotations** — `::error file=…,line=…::` workflow commands, shown in the run's Annotations panel and inline on a
  pull request's diff (capped at 10 errors per step).

Coverage history, per-line diff coverage on pull requests, and a badge need a coverage service.

Locally, the gate also mis-measured: `dotnet test` wrote into each test project's `TestResults/<guid>/` folder, which
accumulates every past run, and the report merged `**/coverage.cobertura.xml` — so a developer's coverage figure was a
blend of every run ever made on that machine.

## Decision

**Results are presented with GitHub's native job summaries and annotations, plus Codecov — and the gate itself stays
read-only.**

- **One in-house JUnit reporter for both tiers.** [`build/test-report.ps1`](../../build/test-report.ps1) reads the JUnit
  files the runs already write (.NET JunitXml.TestLogger and Vitest share the Ant JUnit shape), prints a console digest
  everywhere, and on GitHub appends one combined table plus the failed tests to the job summary and annotates each
  failure at its first in-repository stack frame. It never changes the verdict: the runner's exit code stays the gate.
- **One coverage presenter for both tiers.** [`build/coverage-report.ps1`](../../build/coverage-report.ps1) merges
  Cobertura with ReportGenerator (a pinned local tool) into a repository-relative `Cobertura.xml`, a browsable HTML
  report, a text summary, and a GitHub markdown summary. Vitest's per-package reports are first re-labelled by workspace
  package (istanbul names every package `main` and every class by its bare file name, which would collide on merge).
- **The gate reports its own stages.** `dod.ps1` runs each stage in a collapsible log group, annotates the stage that
  broke, and puts a stage/duration table at the top of the job summary. The gate is identical everywhere; only this
  presentation adapts when `GITHUB_ACTIONS=true`.
- **Every run's results are isolated.** The gate writes test output to `artifacts/test-results/` and clears it first;
  the coverage directory is cleared before it is regenerated.
- **Codecov uploads run in a separate job, over OIDC.** The reusable
  [`codecov-upload.yml`](../../.github/workflows/codecov-upload.yml) job holds the only `id-token: write` permission,
  downloads the gate's artifacts, and uploads coverage and test results with a commit-SHA-pinned Codecov action. Gate
  jobs stay `contents: read`, and every workflow's checkout except `copilot-setup-steps.yml`'s sets
  `persist-credentials: false`, so the job's token never lands in `.git/config`, where build, test and third-party
  action code could read it. No upload secret is stored; pull requests from forks use Codecov's tokenless fork path.
- **Codecov reports; it does not gate.** Its project and patch statuses are informational
  ([`codecov.yml`](../../codecov.yml)) and an upload failure never fails a run, so the required check stays
  "Definition of Done". The path-filtered frontend flag carries forward across commits that do not touch it.

## Options considered

- **GitHubActionsTestLogger** (a VSTest/MTP logger NuGet package) — mature, and it annotates at the test's source
  line. Rejected: a new third-party test dependency that serves only the .NET tier, producing one summary section per
  assembly (22) rather than a single table, and whose README attaches non-license terms to its use. The in-house
  reporter is one small script that both tiers share and that reads the JUnit files already uploaded and sent to
  Codecov.
- **Check-run / PR-comment actions** (for example `dorny/test-reporter`, `EnricoMi/publish-unit-test-result-action`) —
  rejected: they need `checks: write` / `pull-requests: write`, which pull requests from forks do not get without
  splitting the workflow into a privileged `workflow_run` follow-up. Job summaries and annotations cover the same need
  with no write permission at all.
- **A token-authenticated Codecov upload inside the gate job** — rejected: it stores a long-lived secret and exposes
  the token (or an OIDC-minting permission) to third-party build code. A separate job with OIDC has neither exposure.
- **Vitest-native coverage presentation for the frontend** (istanbul HTML per package, a hand-built table) — rejected:
  eleven separate HTML reports and a second presentation style. Installing .NET in the frontend job purely to run
  ReportGenerator is the price of one merged report rendered the same way as the .NET tier's.
- **Coverage thresholds as a gate** — out of scope: reporting coverage is not deciding a coverage policy.

What the figure measures, and how a partially covered line counts, is set by
[ADR-0185](0185-coverage-measures-hand-written-code-and-counts-a-line-that-runs-as-covered.md).

## Consequences

- A run page shows the verdict, each stage's duration, test totals per assembly/package, failed tests with their
  messages, and coverage — and the Annotations panel points at the failing test line and the broken stage.
- Codecov shows coverage history, per-line coverage on pull-request diffs, flaky/failed-test analytics, and the README
  badge. The repository must be activated on codecov.io and the Codecov GitHub App installed on the organization for
  uploads, statuses, and comments to work; without it the upload job logs a non-fatal failure.
- Dependabot-triggered runs may not receive an OIDC token, so those pull requests can lack Codecov data; that is
  accepted, because Codecov is not a gate.
- Coverage figures change once stale runs stop being merged; the first figures after this change are the first
  accurate ones. Old `tests/*/TestResults/` folders on a developer machine are no longer read and can be deleted.
- `@vitest/coverage-v8` must track the installed Vitest version, so it moves with every Vitest upgrade (issue [#146](https://github.com/NCodeGroup/Identity/issues/146)).
- ReportGenerator's markdown is trimmed to its overall summary if it would exceed GitHub's 1 MiB step-summary limit.

## References

- [`build/dod.ps1`](../../build/dod.ps1), [`build/test-report.ps1`](../../build/test-report.ps1),
  [`build/coverage-report.ps1`](../../build/coverage-report.ps1)
- [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml),
  [`.github/workflows/frontend.yml`](../../.github/workflows/frontend.yml),
  [`.github/workflows/codecov-upload.yml`](../../.github/workflows/codecov-upload.yml), [`codecov.yml`](../../codecov.yml)
- GitHub: [job summaries and workflow commands](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands),
  [OpenID Connect in GitHub Actions](https://docs.github.com/en/actions/concepts/security/openid-connect)
- Codecov: [codecov-action](https://github.com/codecov/codecov-action) (`use_oidc`, `report_type: test_results`)
