# 263. Feature branches run a fast gate and the merge queue runs the full gate

- **Status:** Accepted
- **Date:** 2026-10-25
- **Deciders:** NCode Group

## Context

[`build/dod.ps1`](../../build/dod.ps1) is the one Definition of Done that developers, agents and CI run. Its cost is
almost all tests: measured with runtime-bound minimal-API delegates, the local fast gate takes about 7 minutes, of which
the test stage is about 6 minutes (about 82%). CI's full gate takes about 8 minutes on a 4-core runner: about 6 minutes
of tests and 1m23s of build plus packing. With compile-time delegates
([ADR-0262](0262-minimal-api-request-delegates-are-generated-at-compile-time.md)) the local fast gate takes about 1m36s
with all tests, but its cost still grows with the whole suite, not with the change.

Most changes touch a few projects. A change to `src/NCode.Time` reaches 5 test projects through the project graph, and a
docs-only change reaches none, yet every gate runs every test.

[ADR-0261](0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) requires a pull request into
`main` to pass its checks on a branch up to date with `main`. That strict rule makes every pull request re-run the whole
gate after each update of its branch, and each merge puts every other open pull request behind `main` again. The rule
exists for a real reason: two pull requests, each green against an older `main`, can break `main` together.

## Decision

**A feature branch runs a gate sized to its change, and the full gate runs on the merged result in the merge queue,
on `main` and on the release lines.**

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
   - The changed paths are `git diff --name-only --no-renames` against the merge base of `HEAD` and `-Base`, which
     compares with the working tree and so counts committed, staged and unstaged changes, plus untracked files.
   - A global build input selects all tests: anything under `build/` or `.config/`, `global.json`, `nuget.config`,
     `version.json`, `NCode.Identity.slnx`, `.editorconfig`, `BannedSymbols.txt`, any `Directory.*` file and any
     `.runsettings` file.
   - Every other path maps to the `.csproj` under `src/` or `tests/` whose directory is its longest prefix. A path under
     no project (`docs/`, `charts/`, `src/frontend/`, `.github/`) selects nothing; the checks of Frontend CI, the host
     chart and the host image gate those paths.
   - The selected tests are the reverse `ProjectReference` closure of the mapped projects, restricted to `tests/`.
4. **Each place runs one gate tier.**

   | Where | Command | Tests | Coverage | Packing and advisories |
   | --- | --- | --- | --- | --- |
   | Inner loop (local) | `./build/dod.ps1 -Fast -Filter <expr>` | The matching tests | No | No |
   | Pre-push (local) | `./build/dod.ps1 -Fast -Affected` | The affected test projects | No | No |
   | Pull request into `main` (CI) | `./build/dod.ps1 -Affected -Base <base sha>` | The affected test projects | Yes, of the tests that run | Yes |
   | Merge queue, push to `main`, pull request into or push to `release/**` (CI) | `./build/dod.ps1` | All | Yes | Yes |

   "Advisories" means NuGet vulnerability advisories (NU1901–1904) fail the build; `-Fast` reports them as warnings.
   Compile warnings are errors in every tier.
5. **Every workflow that reports a required check runs in the merge queue and on the release lines.** `.NET CI`,
   `Frontend CI`, `Host chart` and `Host image` trigger on `merge_group` and on pushes to and pull requests into
   `release/**`. Frontend CI's `Detect frontend changes` job runs the frontend job for every event other than
   `pull_request`, so the queue runs it in full. The Codecov uploads are skipped on `merge_group`: the queue's run
   duplicates the coverage that `main`'s push uploads.
6. **Codecov's pull-request comment hides project coverage** (`comment.hide_project_coverage: true` in
   [`codecov.yml`](../../codecov.yml)). A pull request's run covers only the affected tests, so only its patch coverage
   is meaningful; `main`'s full runs keep reporting project coverage.
7. **Three repository rulesets protect the trunk, the release lines and the release tags.** None has bypass actors.
   - `main` (the default branch): a pull request with 0 required approvals; the four required checks
     `Definition of Done`, `Lint, typecheck, test, build`, `Lint and render the host chart` and `Build host image`,
     **without** requiring the branch to be up to date; a merge queue that squash-merges, groups only all-green entries,
     builds at most 5 entries per group and times a check out after 60 minutes; linear history; no force push and no
     deletion.
   - `release-lines` (`refs/heads/release/v*`): a pull request, the same four checks with the branch required to be up
     to date, linear history, no force push and no deletion, and no merge queue. A merge queue does not apply to a
     wildcard branch pattern, and a release line takes few changes.
   - `release-tags` (`refs/tags/v*`): a tag can be created but not deleted or moved.
8. **Servicing is upstream-first.**
   - A fix merges to `main` first. A backport pull request cherry-picks it with `git cherry-pick -x` onto a branch
     `backport/<pr>-vX.Y` cut from `release/vX.Y`, and targets that release line.
   - A fix that cannot apply to `main` (for example, because `main` no longer has the code it fixes) lands directly on
     the release line by pull request, whose description states why.
   - A patch release is an annotated `vX.Y.Z` tag on the release line. A hotfix follows the same flow, expedited.

This decision supersedes ADR-0261's requirement that a pull request into `main` pass its checks on a branch up to date
with `main`; the merge queue gives the same guarantee by running the full gate on the merged result. The rest of
ADR-0261 stands.

## Options considered

- **Keep the full gate and the strict rule on every pull request.** Rejected: every pull request pays the whole suite
  after every update of its branch, whatever it changed.
- **Shard the test suite across CI runners.** Rejected: it shortens the wall time of a run but not the work. Each shard
  repeats the restore and build, so a pull request uses more runner time, and the strict rule still re-runs every shard
  after each update.
- **Path-filter the workflows.** Rejected: a required check whose workflow does not run stays pending forever
  (ADR-0261), and a workflow path filter cannot follow `ProjectReference` edges to the tests a change reaches.
- **Drop the strict rule without a merge queue.** Rejected: it merges combinations that no gate has run. Two pull
  requests, each green against an older `main`, can break `main` together, and nothing would catch it before the push.
- **A fast gate on feature branches and the full gate in the merge queue (chosen).** A pull request pays for what it
  changes, and `main` still receives only commits whose merged result passed the full gate.

## Consequences

- A pull request runs only the tests its change reaches: a change to `src/NCode.Time` runs 5 test projects, and a
  docs-only change runs none. A pull request no longer needs updating after every merge to `main`.
- A merge waits in the merge queue for the full gate on the merged result. A dependency edge that `-Affected` misses,
  or a pair of pull requests that break each other, fails the queue's full gate before it reaches `main`.
- A pull request into `main` may skip its tests and its coverage report; its required check still reports, and Codecov
  shows only patch coverage.
- A pull request into a release line runs the full gate and must be up to date with its line before it merges.
- A workflow added later whose job is a required check must trigger on `merge_group` and on the release lines, or the
  queue and the release-line pull requests wait for it forever.
- A new global build input that is not in the trigger pattern of `affected-tests.ps1` is the smell this decision is
  exposed to: a pull request would run too few tests, and only the queue would catch the break. Add it to the pattern.
- Backports are cherry-picked by hand; backlog `X7` tracks automating them.

## References

- [ADR-0261](0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) — the protected trunk, whose
  strict up-to-date requirement this supersedes.
- [ADR-0262](0262-minimal-api-request-delegates-are-generated-at-compile-time.md) — the compile-time delegates that
  shorten the test stage.
- [ADR-0125](0125-trunk-based-development-on-main.md) — trunk-based development and the release lines.
- [ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md) — how CI presents test results and coverage.
- [ADR-0185](0185-coverage-measures-hand-written-code-and-counts-a-line-that-runs-as-covered.md) — the coverage scope.
- [`build/dod.ps1`](../../build/dod.ps1) and [`build/affected-tests.ps1`](../../build/affected-tests.ps1) — the gate and
  the selector; [`build/tests/affected-tests.tests.ps1`](../../build/tests/affected-tests.tests.ps1) — its tests.
- [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), [`frontend.yml`](../../.github/workflows/frontend.yml),
  [`host-chart.yml`](../../.github/workflows/host-chart.yml) and
  [`host-image.yml`](../../.github/workflows/host-image.yml) — the workflows that report the required checks.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — change control, release lines, tags and servicing.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the day-to-day contributor workflow.
