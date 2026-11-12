# Contributing to NCode.Identity

Thanks for helping improve NCode.Identity. These are security-relevant libraries built for publication, so they hold to an explicit,
enforced quality bar.

## Prerequisites

- The .NET SDK that [`global.json`](global.json) selects, 10.0.403 or a later .NET 10 SDK (an older one stops the build
  at once and names the version it needs), and the runtime of each older .NET a test project targets, which
  `./build/Get-TestRuntimes.ps1` names. `./build/Install-Dotnet.ps1` installs both beside the .NET that runs `dotnet`,
  or into the folder `-InstallDir` names; run it as an administrator, or with `sudo`, when .NET is installed
  system-wide. The net10 targeting pack restores automatically.
- PowerShell 7.4+ (`pwsh`) to run the Definition-of-Done script and `./build/Install-Dotnet.ps1`.
- Local tools: run `dotnet tool restore` once after cloning to install the repo-pinned **CSharpier** formatter.
- Git hooks: run `git config core.hooksPath .githooks` once after cloning to enable the repo-versioned
  `.githooks/pre-push` hook, which refuses a push to the default branch when the Landing section of `AGENTS.md` says
  `protect: hook`.
- Windows: run `git config --global core.longpaths true` once. A Claude Code plan's worktree under
  `.claude/worktrees/` adds enough depth that some paths pass Windows' 260-character limit, and without it
  `git worktree remove` fails with "Filename too long" and leaves the folder behind.
- Frontend / full-stack (Aspire F5): [NVS](https://github.com/jasongin/nvs); `build/frontend-install.ps1` adds the
  Node major pinned in `frontend/.node-version` and the pinned pnpm — see
  [`frontend/README.md`](frontend/README.md).
- End-to-end suites only: a running Docker engine. `./build/e2e.ps1` installs the Playwright browsers.
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
([ADR-1143](docs/decisions/1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)). The frontend workspace's equivalent is
`pnpm run test:report` (in `frontend`), which adds per-package JUnit and coverage output to `pnpm test`.

The Definition of Done gates the .NET tier only. A change under `frontend` also runs that workspace's gates before
it is pushed — `pnpm format:check`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, and `pnpm build` in `frontend` —
the same gates CI's frontend workflow runs. Every text file is LF with a final newline (`.editorconfig`,
`.gitattributes`), and the format gates read the working tree, not the normalized index: a file an editor or tool wrote
with CRLF fails Prettier until it is converted.

To preview the CI presentation (job summary + annotations) without pushing, run the gate with `GITHUB_ACTIONS=true` and
point `GITHUB_STEP_SUMMARY` at an empty temporary file, then read that file. The build writes nothing to `GITHUB_ENV`:
`version.json` turns off Nerdbank.GitVersioning's cloud-build variables, whose unlocked appends from parallel MSBuild
nodes tore that file and failed the job
([ADR-1003](docs/decisions/1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)).

When adding a project, place it in the solution folder that mirrors its directory — `/src/` for `src/**`, `/hosts/` for `hosts/**`, `/analyzers/` for `analyzers/**`, `/tests/` for `tests/**` and `/benchmarks/` for `benchmarks/**` (e.g. `dotnet sln add src/Foo/Foo.csproj --solution-folder src`)
— never at the solution root. A published package goes under `src/` and packs; any other project goes under the folder of its role, whose `Directory.Build.props` turns packing off, and the build fails a project whose folder and packing disagree ([ADR-1501](docs/decisions/1501-a-projects-top-level-folder-and-name-say-whether-it-ships-and-what-it-is.md)).

## Benchmarks

The EdDsa primitives are measured by `benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks` (BenchmarkDotNet). Its results go
to `BenchmarkDotNet.Artifacts/` (gitignored). A pull request that optimizes a primitive quotes the before and after
tables from the same machine. CI and the Definition of Done only compile the project
([ADR-1386](docs/decisions/1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)).

```pwsh
# Measure one class (any class-name pattern):
dotnet run -c Release --project benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks -- --filter '*Fe25519*'
# List the benchmarks without running them:
dotnet run -c Release --project benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks -- --list flat
# A quick smoke run of every benchmark (exits non-zero when one fails):
dotnet run -c Release --project benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks -- --filter '*' --job Dry
```

## End-to-end suites

The `*.EndToEnd.Tests` projects under `tests/` run the backend-for-frontend (BFF) in real browsers and against an
OpenID Provider that is not NCode. `NCode.Identity.Bff.EndToEnd.Tests` hosts the BFF on Kestrel over HTTPS and drives
Chromium, Firefox and (off Windows) WebKit with Playwright for .NET. Its browser suite checks the session cookie's
attributes, `SameSite` on cross-site navigation, CORS preflight denial and Fetch Metadata rejection. Its interop suite
runs the BFF against Keycloak in a container and checks login, DPoP, refresh-token rotation, RP-initiated logout and
back-channel logout. They need a running Docker engine.

The suite also runs a Blazor Web App (interactive server and WebAssembly) from the support projects
`NCode.Identity.Bff.EndToEnd.BlazorApp` and `NCode.Identity.Bff.EndToEnd.BlazorApp.Client` through
`WebApplicationFactory` on Kestrel. It checks that a circuit signs out after back-channel logout, that a `WebSocket`
from another origin is refused, that an idle hub connection closes once its session ends, and that a WebAssembly
component's traffic carries no token. A project under `tests/` whose name does not end in `.Tests` is a support project:
the solution compiles it and no gate tests it
([ADR-1441](docs/decisions/1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md)).

```pwsh
# Run every end-to-end suite (installs the Playwright browsers and pulls the Keycloak image on the first run):
./build/e2e.ps1
# Run only the suites the branch's changes reach (against origin/main):
./build/e2e.ps1 -Affected
# Run the tests that match a dotnet test filter:
./build/e2e.ps1 -Filter 'FullyQualifiedName~SessionCookieTests'
```

The `End-to-end` CI check runs `./build/e2e.ps1`: with `-Affected` on a pull request, and in full in the merge queue, on
`main` and on the release lines. The Definition of Done and a bare `dotnet test NCode.Identity.slnx` differ: the
Definition of Done never runs the suites, while a bare `dotnet test` runs them and so needs Docker
([ADR-1430](docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).

## Coding standards

**The coding-convention instruction files are the authoritative checklist**, auto-applied by scope: the project
template's [`csharp.instructions.md`](.github/instructions/csharp.instructions.md) and
[`csharp-tests.instructions.md`](.github/instructions/csharp-tests.instructions.md) hold the general C# rules, and
[`csharp-production.instructions.md`](.github/instructions/csharp-production.instructions.md) and
[`csharp-testing.instructions.md`](.github/instructions/csharp-testing.instructions.md) hold this family's own, each
marked tool-enforced or review-only. Every rule lives in one of them, once. The highlights:

- `ValueTask` / `ValueTask<T>` for async; `Async` suffix; `CancellationToken` last.
- Nullable enabled; restructure so the compiler proves non-null instead of using `!`.
- `sealed` / `init` / concrete collections; vocabularies are open by default — `const string` names plus registered
  descriptors or strategies — and an `enum` only for a provably closed set
  ([ADR-1150](docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md)).
- No behavior-selecting flag arguments; split into intention-revealing methods instead.
- **No cross-assembly `InternalsVisibleTo`** in production — packages compose through public contracts; a package
  grants only its `.Tests` project, `DynamicProxyGenAssembly2` and its `.Benchmarks` project
  ([project-template ADR-0104](docs/decisions/project-template/0104-no-cross-assembly-internalsvisibleto.md)).
- One type per file; the single canonical implementation of `IFoo` is `DefaultFoo`
  ([project-template ADR-0105](docs/decisions/project-template/0105-the-canonical-implementation-is-named-defaultfoo.md)); C# 14 `extension` syntax.

## Making a change

1. Open (or find) a GitHub issue describing the problem or proposal. A change that affects the **public outcome**
   (semantics, public surface, validation/security posture) should reach agreement on the issue — and be recorded as an
   ADR under [`docs/decisions/`](docs/decisions/) — before the pull request.
2. Work on a short-lived branch from `main`. Keep the public surface deliberate; from the first stable release, a
   breaking change to the public API is a **major** version bump.
3. Keep [`CHANGELOG.md`](CHANGELOG.md) matching the code. While `AGENTS.md`'s Landing section says
   `pre-release: untracked` and no version tag of 1.0.0 or later exists, its `## [Unreleased]` section describes the
   family as it is, under `### Added` and `### Security` only: edit the entry a change adds, reshapes or removes
   ([project-template ADR-0015](docs/decisions/project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)).
4. Ensure `./build/dod.ps1 -Fast -Affected` passes before pushing.
5. Push the branch and open a pull request against `main`, as a draft while it is in progress. The pull request runs
   the fast gate: the `Definition of Done` check runs only the tests its change reaches. Once the required checks pass
   and every review thread is resolved, merging ("Merge when ready") adds it to the merge queue, which runs the full
   gate on the merged result and squash-merges it, so the branch need not be up to date with `main`; no one pushes to
   `main` directly. See
   [ADR-1261](docs/decisions/1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md) and
   [ADR-1263](docs/decisions/1263-branches-and-pull-requests-run-an-affected-gate-and-the-merge-queue-runs-the-full-gate.md). CI runs on a
   pull request, in the merge queue, and on a push to `main` or a release line, not on a push to any other branch, so a
   branch's commits are checked once. To check a branch that has no pull request, open a draft pull request or run the
   workflows by hand (`workflow_dispatch`).

An agent-assisted change of more than a few steps runs as **plan-and-execute**: Claude Opus plans it into `PLAN.md`,
Claude Sonnet 5.5 executes one verified step at a time, and Opus re-plans any step that comes back — in VS Code,
Copilot CLI or Claude Code. The workflow files come from the project template; update them with
`/plan-template-update`, which plans and runs `./.template/Update-Project.ps1`, never by hand. See
[AGENTS.md](AGENTS.md#plan-and-execute-workflow) and
[ADR-1170](docs/decisions/1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md).

See [GOVERNANCE.md](GOVERNANCE.md) for ownership, the release lifecycle, and how versions map onto tags.

## Commit attribution

A commit records the **people** who authored it. **Avoid agent attribution in a commit message** — a
`Co-authored-by:` trailer naming an agent account (`Copilot`, `copilot-swe-agent[bot]`, Claude's
`noreply@anthropic.com`, or any other tooling identity), a `Claude-Session:` trailer, or a "Generated with" line: the
repository host reads such a trailer as authorship and lists the agent in the contributor graph, which can only be
undone by rewriting published history. Which tools produced a change belongs in the pull request, not in the commit's
authorship record.

The editor settings keep the tools from adding it: `attribution` in [`.claude/settings.json`](.claude/settings.json)
keeps Claude Code's commit and pull request attribution empty, and `git.addAICoAuthor` is `off` in
[`.vscode/settings.json`](.vscode/settings.json). Nothing checks for it or blocks a commit, push or pull request that
has it, so one a tool adds anyway needs no fix. See
[project-template ADR-0018](docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md).

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
per project ([ADR-1003](docs/decisions/1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)).

- A `<PackageVersion Include="X" Version="1.2.3" />` in `Directory.Packages.props` says **which version**.
- A `<PackageReference Include="X" />` (no `Version`) in a `.csproj` says **which project uses it**. NuGet errors with
  `NU1008` if a `PackageReference` carries an inline version while CPM is on.
- References every project needs (Nerdbank.GitVersioning) sit as versionless `PackageReference`s in
  [`Directory.Build.props`](Directory.Build.props); provenance/analyzer references sit in
  [`Directory.Build.targets`](Directory.Build.targets), gated on `IsPackable`.

To upgrade a package, change its single `Version` in `Directory.Packages.props` — every consuming project moves together.

## Dependabot pull requests

[`.github/dependabot.yml`](.github/dependabot.yml) groups updates per ecosystem (NuGet minor/patch, NuGet majors,
frontend dev toolchain, frontend production, GitHub Actions, the .NET SDK floor, base images). CI gates them like any
other change, so a green Dependabot PR can be merged as-is. **Weekly version updates are paused** while the family is
refactored ahead of its first release (issue [#36](https://github.com/NCodeGroup/identity/issues/36)): every other
ecosystem sets `open-pull-requests-limit: 0`. Security updates still arrive as pull requests (one group per ecosystem)
whenever a published advisory affects a dependency. The docker and dotnet-sdk ecosystems keep their limit of 5 and run
during the pause: a digest pin and the SDK floor in `global.json` follow their upstream releases
([ADR-1248](docs/decisions/1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md),
[ADR-1493](docs/decisions/1493-global-json-requires-the-newest-net-sdk-release-past-the-cooldown.md)).

When one is red, the fix belongs **on its branch**: check out the PR branch, commit the code or config the new versions
require, rebase onto `main` if it has moved, and push. Dependabot stops rebasing a branch once someone else has pushed
to it, which is what you want: it would otherwise discard your fix. Do not close it to get a fresh one; the next run
would just bring back the same break.

When the **update job itself** fails (the "Dependabot Updates" workflow run, before any pull request exists), its log
names the exact command it ran (`gh run view <run-id> --log`; search for `with command:`). To reproduce a pnpm failure,
run that command in a copy of `frontend` with `--config.cache-dir` and `--config.store-dir` pointing at empty
directories. Dependabot starts with no cache, and a warm local metadata cache can hide the failure. Pushing a change to
`dependabot.yml` starts an update run for every ecosystem right away, so expect one when you change it.

When an upgrade **cannot land yet** (the upstream ecosystem has not caught up), hold it with an `ignore` rule in
`dependabot.yml` that explains why, plus an issue for lifting it. That is how TypeScript majors and `@types/node`
majors are held today
([ADR-1152](docs/decisions/1152-the-frontend-pins-node-and-pnpm-once-gates-dependencies-by-release-age-and-tracks-supported-upstreams.md)). Without the rule,
every weekly group PR fails on the same package.

The frontend has no lockfile-free path: after changing a `package.json`, run `pnpm install` in `frontend` and commit
`pnpm-lock.yaml`, because CI installs with `--frozen-lockfile`. Frontend versions must be three days old before anyone
installs them: pnpm's `minimumReleaseAge` gates people and Dependabot's npm `cooldown` gates its updates. Keep the two
windows equal, because Dependabot fails while the lockfile holds a version younger than its cooldown
([ADR-1152](docs/decisions/1152-the-frontend-pins-node-and-pnpm-once-gates-dependencies-by-release-age-and-tracks-supported-upstreams.md)).

Workflows reference GitHub's own `actions/*` by major tag (`actions/checkout@v7`) and every **third-party** action by
full commit SHA with its version in a trailing comment
(`pnpm/action-setup@ea17c68df8912ef543352723c149a84f56e3d413 # v6.1.0`). A tag can be moved to different code after
review; a commit cannot. Dependabot's GitHub Actions group updates the SHA and the comment together.

## Public API surface guard (`PublicAPI.*.txt`)

Because these libraries are built for publication, an accidental change to the public API surface would break every
consumer. The
[`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
Roslyn analyzer makes the surface explicit and reviewable via a `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
pair per packable project ([ADR-1025](docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)):

- Add public API → add its lines to `PublicAPI.Unshipped.txt` (the IDE's _"Add to public API"_ code fix generates them).
- Remove public API → prefix the removed line with `*REMOVED*` in `PublicAPI.Unshipped.txt`.
- At release → move everything from `Unshipped` into `Shipped`. Pre-1.0, the whole surface stays in `Unshipped`.

Reviewing a PR then includes reviewing the diff of these files. See
[`docs/api-compatibility.md`](docs/api-compatibility.md) for the full backward-compatibility procedure.
