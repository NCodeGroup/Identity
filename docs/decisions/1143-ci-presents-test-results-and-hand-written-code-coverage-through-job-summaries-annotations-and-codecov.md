# 1143. CI presents test results and hand-written-code coverage through job summaries, annotations and Codecov

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

Both CI tiers produce rich results: the .NET gate ([`build/dod.ps1`](../../build/dod.ps1)) writes a JUnit report per
test assembly and a merged Cobertura coverage report, and the frontend workspace runs Vitest across every package.
Uploading those files only as zip artifacts leaves a run page that says nothing beyond "Success": no test counts, no
coverage, no failed test located in the code, no indication of which stage of the gate broke.

GitHub has no built-in JUnit or Cobertura viewer, but it renders two things natively, with no write permission:

- the **job summary**, markdown a step writes to `$GITHUB_STEP_SUMMARY`, shown on the run page and on a pull request's
  checks; and
- **annotations**, `::error file=…,line=…::` workflow commands, shown in the run's Annotations panel and inline on a
  pull request's diff (capped at 10 errors per step).

Coverage history, per-line diff coverage on pull requests and a badge need a coverage service.

Locally, a figure can mis-measure. `dotnet test` writes into each test project's `TestResults/<guid>/` folder, which
accumulates every past run, and a report that merges `**/coverage.cobertura.xml` blends every run ever made on that
machine into one number.

What the figure counts is a second force. Coverlet measures every source file in every assembly a test loads, which
includes code nobody writes by hand:

- source-generator output under `obj/`, such as the OpenAPI XML-comment support and `GeneratedRegex` implementations;
- EF Core migrations under `NCode.Identity.OpenId.Host.Database/Migrations/`, which `dotnet ef` scaffolds.

The generated files are build intermediates, not repository files. ReportGenerator warns that they do not exist, and
Codecov drops them silently, so the two presenters disagree about which lines count. They also disagree about how a line
counts: ReportGenerator counts a line whose branches are only partly taken as covered, while Codecov counts it as a miss
by default.

## Decision

**Results are presented with GitHub's native job summaries and annotations, plus Codecov, and the gate itself stays
read-only. The headline coverage figure measures hand-written code, and a line that runs counts as covered, in the job
summary and in Codecov alike.**

### Presentation

1. **One in-house JUnit reporter for both tiers.** [`build/test-report.ps1`](../../build/test-report.ps1) reads the JUnit
   files both tiers write (.NET's `JunitXml.TestLogger`, referenced in `tests/Directory.Build.props`, and Vitest share
   the Ant JUnit shape). It prints a console digest everywhere, and on GitHub it writes one combined table and the
   failed tests to the job summary and annotates each failure at its first in-repository stack frame. It never changes
   the verdict, which stays the runner's exit code.
2. **One coverage presenter for both tiers.** [`build/coverage-report.ps1`](../../build/coverage-report.ps1) merges
   Cobertura with ReportGenerator, a pinned local tool, into a repository-relative `Cobertura.xml`, an HTML report, a
   text summary and a markdown summary. Vitest's per-package reports are first relabeled by workspace package, because
   istanbul names every package `main`. The markdown is trimmed to its overall summary when it would exceed GitHub's
   1 MiB step-summary limit.
3. **The gate reports its own stages.** `dod.ps1` runs each stage in a collapsible log group, annotates the stage that
   broke and puts a stage/duration table at the top of the job summary. The gate is identical everywhere; only this
   presentation adapts when `GITHUB_ACTIONS=true`.
4. **Every run's results are isolated.** Every run writes test output to a cleared `artifacts/test-results/`, and
   coverage is regenerated into a cleared directory, so a figure never blends earlier runs.

### Codecov

5. **Uploads run in a separate job, over OIDC.** The reusable
   [`codecov-upload.yml`](../../.github/workflows/codecov-upload.yml) job, called by `ci.yml` and `frontend.yml` with
   `id-token: write`, never runs inside a gate job. It uploads coverage and test results with a commit-SHA-pinned
   action, stores no secret, and fork pull requests use Codecov's tokenless path.
6. **Gate jobs cannot leak the job token.** Gate jobs are `contents: read`, and every checkout sets
   `persist-credentials: false`, so the job token never lands in `.git/config` where build, test and third-party code
   could read it. The exceptions are the checkouts of jobs that push with that token: `backport.yml` and the template's
   `tag-release.yml` tagging job.
7. **Codecov reports and never gates.** Its project and patch statuses are informational in
   [`codecov.yml`](../../codecov.yml), and an upload failure never fails a run, so the required check stays "Definition
   of Done". The path-filtered frontend flag carries forward across commits that do not touch it.

### Coverage scope

8. **Generated code is excluded where coverage is collected.** [`build/coverage.runsettings`](../../build/coverage.runsettings)
   makes coverlet skip files under `obj/` and `Migrations/` and members marked `[GeneratedCode]`; coverlet already
   skips `[ExcludeFromCodeCoverage]`. `dod.ps1` passes the file whenever it collects coverage, so one setting scopes both
   presenters, which read the reports it produces.
9. **`[CompilerGenerated]` stays in scope.** The compiler marks async state machines, lambdas and iterators with it, so
   excluding it would drop hand-written bodies.
10. **Hand-written code stays in scope wherever it lives.** The reference host and the host database project ship in the
    image, so a low host figure reports a real gap. Assemblies that no test loads, such as the Aspire orchestrator,
    never enter the report. Hand-written code in a project that also holds scaffolded migrations stays outside
    `Migrations/` ([ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md)).
11. **Codecov counts a partially covered line as a hit.** `codecov.yml` sets `parsers.cobertura.partials_as_hits`,
    matching ReportGenerator's line coverage, for both the .NET and the frontend uploads. Branch coverage stays in the
    report for the partial lines.

## Options considered

- **GitHubActionsTestLogger**, a VSTest/MTP logger NuGet package, is mature and annotates at the test's source line.
  Rejected: it is a third-party test dependency that serves only the .NET tier, it produces one summary section per
  assembly rather than a single table, and its README attaches non-license terms to its use. The in-house reporter is
  one small script that both tiers share and that reads the JUnit files already uploaded and sent to Codecov.
- **Check-run or pull-request-comment actions** need `checks: write` or `pull-requests: write`, which pull requests
  from forks do not get without splitting the workflow into a privileged `workflow_run` follow-up. Rejected: job
  summaries and annotations cover the same need with no write permission at all.
- **A token-authenticated Codecov upload inside the gate job.** Rejected: it stores a long-lived secret and exposes the
  token, or an OIDC-minting permission, to third-party build code. A separate job with OIDC has neither exposure.
- **Per-package Vitest HTML** with a hand-built table. Rejected: it yields a separate HTML report per package and a
  second presentation style. Installing .NET in the frontend job purely to run ReportGenerator is the price of one
  merged report rendered the same way as the .NET tier's.
- **ReportGenerator filters plus Codecov `ignore` lists.** Rejected: two filter lists in two syntaxes must be kept
  equal, while excluding at collection gives both presenters the same input from one list.
- **Excluding the host assemblies from the headline.** Rejected: the host is shipped code, so leaving it out hides a gap
  that matters.
- **Chosen: native job summaries and annotations from one in-house reporter, a separate OIDC Codecov job that never
  gates, and generated code excluded once at collection.**

## Consequences

- A run page shows the verdict, each stage's duration, test totals per assembly and package, failed tests with their
  messages and coverage, and the Annotations panel points at the failing test line and the broken stage.
- Codecov shows coverage history, per-line coverage on pull-request diffs, flaky and failed-test analytics, and the
  README badge. The repository must be activated on codecov.io and the Codecov GitHub App installed on the organization
  for uploads, statuses and comments to work; without it the upload job logs a non-fatal failure.
- A Dependabot run that gets no OIDC token may lack Codecov data, which is accepted because Codecov is not a gate.
- The job summary and Codecov report the same scope and the same line counting, and the merged report names no file
  under `obj/` or `Migrations/`.
- `./build/dod.ps1 -Fast` collects no coverage, so the coverage setting affects only the full gate that CI runs.
- `@vitest/coverage-v8` must track the installed Vitest version, so it moves with every Vitest upgrade (issue
  [#146](https://github.com/NCodeGroup/identity/issues/146)).
- Codecov sets no coverage threshold; the frontend's per-package thresholds belong to the frontend gate.

## References

- [`build/dod.ps1`](../../build/dod.ps1), [`build/test-report.ps1`](../../build/test-report.ps1),
  [`build/coverage-report.ps1`](../../build/coverage-report.ps1) and
  [`build/coverage.runsettings`](../../build/coverage.runsettings)
- [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml),
  [`.github/workflows/frontend.yml`](../../.github/workflows/frontend.yml),
  [`.github/workflows/codecov-upload.yml`](../../.github/workflows/codecov-upload.yml) and
  [`codecov.yml`](../../codecov.yml)
- [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md): the host ships in the image the hosted platform
  runs.
- GitHub: [job summaries and workflow commands](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands),
  [OpenID Connect in GitHub Actions](https://docs.github.com/en/actions/concepts/security/openid-connect)
- Codecov: [codecov-action](https://github.com/codecov/codecov-action) (`use_oidc`, `report_type: test_results`)
- coverlet's `ExcludeByFile` and `ExcludeByAttribute` collector settings
