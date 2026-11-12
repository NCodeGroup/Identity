# 1263. Branches and pull requests run an affected gate, and the merge queue runs the full gate

- **Status:** Accepted
- **Date:** 2026-10-25
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0005](project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md)

## Context

[`build/dod.ps1`](../../build/dod.ps1) is the one Definition of Done that developers, agents and CI run. Its cost is
almost all tests, and that cost grows with the whole suite, not with the change. Most changes touch a few projects, and
a docs-only change reaches none, yet a gate that runs every test runs them all for each of them.

[ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md)
protects `main` with pull requests and a merge queue. A strict up-to-date rule on every pull request would make each one
re-run the whole gate after every update of its branch, and each merge would put every other open pull request behind
`main` again. The rule exists for a real reason: two pull requests, each green against an older `main`, can break `main`
together. The generated request delegates of
[ADR-1262](1262-minimal-api-request-delegates-are-generated-at-compile-time.md) shorten the test stage but do not change
that it scales with the suite.

## Decision

**A feature branch runs a gate sized to its change, and the full gate runs on the merged result in the merge queue, on
`main` and on the release lines.** A pull request pays for what it changes, while `main` receives only commits whose
merged result passed the full gate.

1. **Test assemblies run in parallel in every mode.** `dod.ps1` passes `RunConfiguration.MaxCpuCount=0` to
   `dotnet test`, the full gate included. Coverlet collects coverage per test-host process, so coverage is unaffected.
2. **`dod.ps1` narrows its tests with two composable switches.**
   - `-Filter <expr>` passes through to `dotnet test --filter`.
   - `-Affected [-Base <commit-ish>]` (default base `origin/main`) runs only the test projects that
     [`build/affected-tests.ps1`](../../build/affected-tests.ps1) selects, through a solution filter in `artifacts/`.
     When it selects none, the test stage is skipped and the gate still passes.
   - The two compose with each other and with `-Fast`. The build stage always builds the whole solution, so a compile
     error anywhere still fails the gate.
3. **`affected-tests.ps1` selects from the project graph.**
   - The changed paths are `git diff --name-only --no-renames` against the merge base of `HEAD` and the base, which
     compares with the working tree and so counts committed, staged and unstaged changes, plus the untracked files that
     `git ls-files --others --exclude-standard` lists.
   - A global build input selects all tests: anything under `build/` or `.config/`, `global.json`, `nuget.config`,
     `version.json`, `NCode.Identity.slnx`, `.editorconfig`, any `BannedSymbols*.txt` file, any `Directory.*` file and
     any `.runsettings` file.
   - Every other path maps to the `.csproj` under `src/`, `hosts/`, `analyzers/` or `tests/` whose directory is its
     longest prefix. A path under no project (`docs/`, `charts/`, `frontend/`, `.github/`) selects nothing; the checks
     of Frontend CI, the host chart and the host image gate those paths.
   - The selected tests are the reverse `ProjectReference` closure of the mapped projects, restricted to `tests/`.
4. **Each place runs one gate tier.**

   | Where | Command | Tests | Coverage | Packing and advisories |
   | --- | --- | --- | --- | --- |
   | Inner loop (local) | `./build/dod.ps1 -Fast -Filter <expr>` | The matching tests | No | No |
   | Pre-push (local) | `./build/dod.ps1 -Fast -Affected` | The affected test projects | No | No |
   | Pull request into `main` (CI) | `./build/dod.ps1 -Affected -Base <base sha>` | The affected test projects | Yes, of the tests that run | Yes |
   | Merge queue, push to `main`, pull request into or push to `release/**` (CI) | `./build/dod.ps1` | All | Yes | Yes |

   "Advisories" means NuGet vulnerability advisories (NU1901–NU1904) fail the build; `-Fast` reports them as warnings.
   Compile warnings are errors in every tier.
5. **Every workflow that reports a required check runs in the merge queue and on the release lines.** `.NET CI`,
   `Frontend CI`, `Host chart`, `Host image`, `Console image` and `End-to-end` trigger on `merge_group` and on pushes to
   and pull requests into `release/**`. Frontend CI's `Detect frontend changes` job runs the frontend job for every event
   other than `pull_request`, so the queue runs it in full. A required check added later must do the same, or the queue
   and the release-line pull requests wait for it. The Codecov uploads are skipped on `merge_group`: the queue's run
   duplicates the coverage that `main`'s push uploads.
6. **Codecov's pull-request comment hides project coverage** (`comment.hide_project_coverage: true` in
   [`codecov.yml`](../../codecov.yml)). A pull request's run covers only the affected tests, so only its patch coverage
   is meaningful; `main`'s full runs keep reporting project coverage.
7. **The end-to-end suites are not part of `dod.ps1`.** `./build/e2e.ps1` and the `End-to-end` check run them in their
   own gate, as [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)
   decides.

## Options considered

- **Keep the full gate and the strict rule on every pull request.** Rejected: every pull request pays the whole suite
  after every update of its branch, whatever it changed.
- **Shard the test suite across CI runners.** Rejected: it shortens the wall time of a run but not the work. Each shard
  repeats the restore and build, so a pull request uses more runner time, and the strict rule still re-runs every shard
  after each update.
- **Path-filter the workflows.** Rejected: a required check whose workflow does not run stays pending forever, and a
  workflow path filter cannot follow `ProjectReference` edges to the tests a change reaches.
- **Drop the strict rule without a merge queue.** Rejected: it merges combinations that no gate has run. Two pull
  requests, each green against an older `main`, can break `main` together, and nothing would catch it before the push.
- **A fast gate on feature branches and the full gate in the merge queue (chosen).** A pull request pays for what it
  changes, and `main` still receives only commits whose merged result passed the full gate.

## Consequences

- A pull request runs only the tests its change reaches, and a docs-only change runs none.
- A merge waits in the merge queue for the full gate on the merged result. A dependency edge that `-Affected` misses,
  or a pair of pull requests that break each other, fails the queue's full gate before it reaches `main`.
- A pull request into `main` may skip its tests and its coverage report; its required check still reports, and Codecov
  shows only patch coverage.
- A pull request into a release line runs the full gate and must be up to date with its line before it merges.
- A workflow added later whose job is a required check must trigger on `merge_group` and on the release lines, or the
  queue and the release-line pull requests wait for it.
- A new global build input that is not in the trigger pattern of `affected-tests.ps1` is the smell this decision is
  exposed to: a pull request would run too few tests, and only the queue would catch the break. Add it to the pattern.
- `build/tests/affected-tests.tests.ps1` tests the selector.

## References

- [ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md)
  — the protected trunk and its merge queue.
- [ADR-1262](1262-minimal-api-request-delegates-are-generated-at-compile-time.md) — the compile-time delegates that
  shorten the test stage.
- [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)
  — the end-to-end gate.
- [ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)
  — how CI presents test results and coverage.
- [`build/dod.ps1`](../../build/dod.ps1) and [`build/affected-tests.ps1`](../../build/affected-tests.ps1) — the gate and
  the selector; [`build/tests/affected-tests.tests.ps1`](../../build/tests/affected-tests.tests.ps1) — its tests.
- [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), [`frontend.yml`](../../.github/workflows/frontend.yml),
  [`host-chart.yml`](../../.github/workflows/host-chart.yml) and
  [`host-image.yml`](../../.github/workflows/host-image.yml) — the workflows that report the required checks.
- [`codecov.yml`](../../codecov.yml) — the pull-request comment setting.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — change control, release lines, tags and servicing.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the day-to-day contributor workflow.
