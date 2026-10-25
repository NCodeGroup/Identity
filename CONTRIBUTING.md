# Contributing to NCode.Identity

Thanks for helping improve NCode.Identity. These are shipped, security-relevant libraries, so they hold to an explicit,
enforced quality bar.

## Prerequisites

- The .NET SDK pinned in [`global.json`](global.json) (10.0.x). The net10 targeting pack restores automatically.
- PowerShell 7+ (`pwsh`) to run the Definition-of-Done script.
- Local tools: run `dotnet tool restore` once after cloning to install the repo-pinned **CSharpier** formatter.
- Git hooks: run `git config core.hooksPath .githooks` once after cloning to enable the repo-versioned hooks (see
  [Commit attribution](#commit-attribution)).
- Windows: run `git config --global core.longpaths true` once. A Claude Code plan's worktree under
  `.claude/worktrees/` adds enough depth that some paths pass Windows' 260-character limit, and without it
  `git worktree remove` fails with "Filename too long" and leaves the folder behind.
- Frontend / full-stack (Aspire F5): [NVS](https://github.com/jasongin/nvs); `build/frontend-install.ps1` adds the
  Node major pinned in `src/frontend/.node-version` and the pinned pnpm — see
  [`src/frontend/README.md`](src/frontend/README.md).
- Conformance harness only: Docker, Python 3, and hosts-file entries — see
  [`build/conformance/README.md`](build/conformance/README.md).

## Build and test

```pwsh
# One-time after cloning — installs pinned local tools (CSharpier).
dotnet tool restore

# One-time after cloning — enables the repo-versioned git hooks.
git config core.hooksPath .githooks

# From the repo root — the full gate, the one the merge queue, main and release lines run in CI
# (CSharpier check + restore + build + pack + all tests + coverage):
./build/dod.ps1

# The local pre-push gate — parallel builds, skips coverage + packing, and runs only the test
# projects the branch's changes reach (against origin/main). Keeps CSharpier, analyzers, and
# warnings-as-errors.
./build/dod.ps1 -Fast -Affected

# The inner loop — the fast gate running only the tests that match a dotnet test filter.
./build/dod.ps1 -Fast -Filter 'FullyQualifiedName~MyTests'

# Or drive the tools directly:
dotnet csharpier format .    # format before committing
dotnet build NCode.Identity.slnx
dotnet test NCode.Identity.slnx
```

Every gate run clears and rewrites `artifacts/test-results/` (one JUnit report per test assembly, plus the raw coverage
of a full run) and prints a test digest and a per-stage duration table; a full run also writes the merged coverage
report to `artifacts/coverage/` — open `index.html` to browse it. In CI the same results appear on the run's summary
page, failed tests and the failing stage are annotated, and coverage is uploaded to Codecov
([ADR-0143](docs/adr/0143-ci-presents-test-results-and-coverage-in-github.md)). The frontend workspace's equivalent is
`pnpm run test:report` (in `src/frontend`), which adds per-package JUnit and coverage output to `pnpm test`.

The Definition of Done gates the .NET tier only. A change under `src/frontend` also runs that workspace's gates before
it is pushed — `pnpm format:check`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, and `pnpm build` in `src/frontend` —
the same gates CI's frontend workflow runs. Every text file is LF with a final newline (`.editorconfig`,
`.gitattributes`), and the format gates read the working tree, not the normalized index: a file an editor or tool wrote
with CRLF fails Prettier until it is converted.

To preview the CI presentation (job summary + annotations) without pushing, run the gate with `GITHUB_ACTIONS=true` and
point `GITHUB_STEP_SUMMARY` at an empty temporary file, then read that file. The build writes nothing to `GITHUB_ENV`:
`version.json` turns off Nerdbank.GitVersioning's cloud-build variables, whose unlocked appends from parallel MSBuild
nodes tore that file and failed the job
([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)).

When adding a project, place it in the solution folder that mirrors its directory — `/src/` for `src/**` and `/tests/`
for `tests/**` (e.g. `dotnet sln add src/Foo/Foo.csproj --solution-folder src`) — never at the solution root.

## Coding standards

**The coding-convention instruction files are the authoritative checklist** — auto-applied by scope
([`csharp-production.instructions.md`](.github/instructions/csharp-production.instructions.md) and
[`csharp-testing.instructions.md`](.github/instructions/csharp-testing.instructions.md)); every rule lives there once,
marked tool-enforced or review-only. The highlights:

- `ValueTask` / `ValueTask<T>` for async; `Async` suffix; `CancellationToken` last.
- Nullable enabled; restructure so the compiler proves non-null instead of using `!`.
- `sealed` / `init` / concrete collections; vocabularies are open by default — `const string` names plus registered
  descriptors or strategies — and an `enum` only for a provably closed set
  ([ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md)).
- No behavior-selecting flag arguments; split into intention-revealing methods instead.
- **No cross-assembly `InternalsVisibleTo`** in production — packages compose through public contracts
  ([ADR-0004](docs/adr/0004-no-cross-assembly-internalsvisibleto.md)).
- One type per file; the single canonical implementation of `IFoo` is `DefaultFoo`
  ([ADR-0006](docs/adr/0006-single-canonical-impl-is-defaultfoo.md)); C# 14 `extension` syntax.

## Making a change

1. Open (or find) a GitHub issue describing the problem or proposal. A change that affects the **public outcome**
   (semantics, public surface, validation/security posture) should reach agreement on the issue — and be recorded as an
   ADR under [`docs/adr/`](docs/adr/) — before the pull request.
2. Work on a short-lived branch from `main`. Keep the public surface deliberate; a breaking change to the public API is
   a **major** version bump.
3. Record any consumer-visible change in [`CHANGELOG.md`](CHANGELOG.md) under `## [Unreleased]`.
4. Ensure `./build/dod.ps1 -Fast -Affected` passes before pushing.
5. Push the branch and open a pull request against `main`, as a draft while it is in progress. The pull request runs
   the fast gate: the `Definition of Done` check runs only the tests its change reaches. Once the required checks pass
   and every review thread is resolved, merging ("Merge when ready") adds it to the merge queue, which runs the full
   gate on the merged result and squash-merges it, so the branch need not be up to date with `main`; no one pushes to
   `main` directly. See
   [ADR-0261](docs/adr/0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) and
   [ADR-0263](docs/adr/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md). CI runs on a
   pull request, in the merge queue, and on a push to `main` or a release line, not on a push to any other branch, so a
   branch's commits are checked once. To check a branch that has no pull request, open a draft pull request or run the
   workflows by hand (`workflow_dispatch`).

An agent-assisted change of more than a few steps runs as **plan-and-execute**: Claude Opus plans it into `PLAN.md`,
Claude Sonnet 5.5 executes one verified step at a time, and Opus re-plans any step that comes back — in VS Code,
Copilot CLI or Claude Code. The workflow files come from the project template; update them with
`./.template/Update-Project.ps1`, never by hand. See [AGENTS.md](AGENTS.md#plan-and-execute-workflow) and
[ADR-0170](docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md).

See [GOVERNANCE.md](GOVERNANCE.md) for ownership, the release lifecycle, and how versions map onto tags.

## Commit attribution

A commit records the **people** who authored it. **Never add agent attribution to a commit message** — a
`Co-authored-by:` trailer naming an agent account (`Copilot`, `copilot-swe-agent[bot]`, Claude's
`noreply@anthropic.com`, or any other tooling identity), a `Claude-Session:` trailer, or a "Generated with" line: the
repository host reads such a trailer as authorship and lists the agent in the contributor graph, which can only be
undone by rewriting published history. Which tools produced a change belongs in the pull request, not in the commit's
authorship record.

Three layers keep the record clean:

- The repo-versioned [`.githooks/commit-msg`](.githooks/commit-msg) hook, which comes from the project template, strips
  such lines automatically once hooks are enabled (`git config core.hooksPath .githooks` — see
  [Prerequisites](#prerequisites)). Hooks are client-side only, so this is a safety net, not a guarantee; don't write
  the lines in the first place.
- The editor settings stop the tools from adding them: `attribution` in [`.claude/settings.json`](.claude/settings.json)
  keeps Claude Code's commit and pull request attribution empty, and `git.addAICoAuthor` is `off` in
  [`.vscode/settings.json`](.vscode/settings.json).
- The required `Commit attribution` check
  ([`.template/Test-CommitAttribution.ps1`](.template/Test-CommitAttribution.ps1), from the project template) runs on
  pull requests, in the merge queue and on pushes to `main` and release lines. It fails when a commit's author or
  committer is an AI agent account, or when a message carries agent attribution, because a squash merge credits every
  commit author.

The Copilot cloud agent authors its commits as `copilot-swe-agent[bot]`, so its pull request fails the check until a
person re-authors the commits and force-pushes the branch:

```shell
git rebase origin/main --exec "git commit --amend --no-edit --reset-author"
git push --force-with-lease
```

See [ADR-0128](docs/adr/0128-commits-are-attributed-to-people-not-agents.md).

## Code formatting — CSharpier

Formatting is owned by [CSharpier](https://csharpier.com), pinned as a **local tool** in
[`.config/dotnet-tools.json`](.config/dotnet-tools.json) and enforced by the Definition of Done
(`dotnet csharpier check .`), so formatting is a build gate, not a review debate.

```shell
dotnet tool restore         # once, after cloning — installs the repo-pinned CSharpier
dotnet csharpier format .   # format everything
dotnet csharpier check .    # verify formatting (what CI runs; fails if anything is unformatted)
```

The version is pinned with `rollForward: false` so formatting output never drifts between CSharpier versions.

## Package versions — Central Package Management (CPM)

Package **versions live in one place** ([`Directory.Packages.props`](Directory.Packages.props)) and are not repeated
per project ([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)).

- A `<PackageVersion Include="X" Version="1.2.3" />` in `Directory.Packages.props` says **which version**.
- A `<PackageReference Include="X" />` (no `Version`) in a `.csproj` says **which project uses it**. NuGet errors with
  `NU1008` if a `PackageReference` carries an inline version while CPM is on.
- References every project needs (Nerdbank.GitVersioning) sit as versionless `PackageReference`s in
  [`Directory.Build.props`](Directory.Build.props); provenance/analyzer references sit in
  [`Directory.Build.targets`](Directory.Build.targets), gated on `IsPackable`.

To upgrade a package, change its single `Version` in `Directory.Packages.props` — every consuming project moves together.

## Dependabot pull requests

[`.github/dependabot.yml`](.github/dependabot.yml) groups updates per ecosystem (NuGet minor/patch, NuGet majors,
frontend dev toolchain, frontend production, GitHub Actions). CI gates them like any other change, so a green
Dependabot PR can be merged as-is. **Weekly version updates are paused** while the family is refactored ahead of its
first release (backlog `D16`): every ecosystem sets `open-pull-requests-limit: 0`. Security updates still arrive as
pull requests (one group per ecosystem) whenever a published advisory affects a dependency.

When one is red, the fix belongs **on its branch**: check out the PR branch, commit the code or config the new versions
require, rebase onto `main` if it has moved, and push. Dependabot stops rebasing a branch once someone else has pushed
to it, which is what you want: it would otherwise discard your fix. Do not close it to get a fresh one; the next run
would just bring back the same break.

When the **update job itself** fails (the "Dependabot Updates" workflow run, before any pull request exists), its log
names the exact command it ran (`gh run view <run-id> --log`; search for `with command:`). To reproduce a pnpm failure,
run that command in a copy of `src/frontend` with `--config.cache-dir` and `--config.store-dir` pointing at empty
directories. Dependabot starts with no cache, and a warm local metadata cache can hide the failure. Pushing a change to
`dependabot.yml` starts an update run for every ecosystem right away, so expect one when you change it.

When an upgrade **cannot land yet** (the upstream ecosystem has not caught up), hold it with an `ignore` rule in
`dependabot.yml` that explains why, plus a backlog item for lifting it. That is how TypeScript majors and `@types/node`
majors are held today
([ADR-0147](docs/adr/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)). Without the rule,
every weekly group PR fails on the same package.

The frontend has no lockfile-free path: after changing a `package.json`, run `pnpm install` in `src/frontend` and commit
`pnpm-lock.yaml`, because CI installs with `--frozen-lockfile`. Frontend versions must be three days old before anyone
installs them: pnpm's `minimumReleaseAge` gates people and Dependabot's npm `cooldown` gates its updates. Keep the two
windows equal, because Dependabot fails while the lockfile holds a version younger than its cooldown
([ADR-0153](docs/adr/0153-one-release-age-window-gates-frontend-dependencies.md)).

Workflows reference GitHub's own `actions/*` by major tag (`actions/checkout@v7`) and every **third-party** action by
full commit SHA with its version in a trailing comment
(`pnpm/action-setup@ea17c68df8912ef543352723c149a84f56e3d413 # v6.1.0`). A tag can be moved to different code after
review; a commit cannot. Dependabot's GitHub Actions group updates the SHA and the comment together.

## Public API surface guard (`PublicAPI.*.txt`)

Because these are shipped libraries, an accidental change to the public API surface is a break for every consumer. The
[`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
Roslyn analyzer makes the surface explicit and reviewable via a `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
pair per packable project ([ADR-0005](docs/adr/0005-public-api-surface-is-tracked-and-evolves-compatibly.md)):

- Add public API → add its lines to `PublicAPI.Unshipped.txt` (the IDE's _"Add to public API"_ code fix generates them).
- Remove public API → prefix the removed line with `*REMOVED*` in `PublicAPI.Unshipped.txt`.
- At release → move everything from `Unshipped` into `Shipped`. Pre-1.0, the whole surface stays in `Unshipped`.

Reviewing a PR then includes reviewing the diff of these files. See
[`docs/api-compatibility.md`](docs/api-compatibility.md) for the full backward-compatibility procedure.
