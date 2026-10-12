# 0071. Adopt .NET Aspire as the local development orchestrator

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

Running the family locally is a **multi-process** exercise: the Playground reference OpenID Provider host
(`api`) plus the frontend surfaces — the Management console Vite dev server (`apps/mgmt`) and the
interactive login/consent build-watcher (`apps/auth`), whose `dist` the Playground serves under `/account/*`
([ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md)). The frontend platform
([ADR-0064](0064-frontend-presentation-platform.md)) is a pnpm + Turborepo workspace with its own toolchain,
deliberately outside the hermetic [`dod.ps1`](../../build/dod.ps1) inner loop.

The prior "one-key" developer experience paired, in a VS Code **compound** launch, the Playground debug
profile with a PowerShell script (`build/dev-ui.ps1`) that ran `pnpm dev` (`turbo run dev`) across every app.
A compound launch starts its members **concurrently with no dependency ordering**. The browser UIs therefore
boot before the API is listening: the Management console's runtime discovery and first management call race
the API and fail in the browser, and the login surface 404s until its first build lands. "Give it a moment
and refresh" was the workaround. The stack also had no single place for ordered startup, aggregated logs, or
traces.

Several invariants in the local setup assume **fixed ports**: the developer seed registers
`https://localhost:5173/callback` as the SPA redirect URI, `AddDeveloperCors` allowlists `localhost:5173`,
and the host-published bootstrap document points the browser SPA at `https://localhost:5001`. Any
orchestration must preserve those ports, not allocate them dynamically.

## Decision

Adopt **.NET Aspire** as the local dev orchestrator through a new **sibling** AppHost project,
[`NCode.Identity.OpenId.Playground.AppHost`](../../src/NCode.Identity.OpenId.Playground.AppHost/). It models
the whole local stack as one resource graph and becomes the single one-stop entry to build, run, and debug
everything (the `Aspire: Full stack (F5)` launch).

Rules:

- **The AppHost is a sibling, never nested in the Playground.** The Playground stays a single reference OP
  **process**; Aspire is a separate orchestration host (one `DistributedApplication`) that _references_ it.
- **UI resources are gated on API _health_, not "process started."** `api` exposes `WithHttpHealthCheck("/health")`
  (the host already maps `/health`); `mgmt` declares `.WaitFor(api)`, so Aspire holds the UI until `/health`
  returns `200`. This deterministically removes the UI-before-API race — the root symptom that motivated this ADR.
- **Ports stay pinned to preserve the dev contract.** `api` keeps the Playground launch profile's
  `https://localhost:5001` / `http://localhost:5000`; `mgmt` is pinned to `http://localhost:5173`, direct and
  un-proxied (`IsProxied = false`) so Vite HMR and the OAuth authorization-code callback work and the seed/CORS
  origins stay valid. Determinism is chosen over Aspire's default dynamic port allocation precisely because the
  seed, CORS, and bootstrap document assume those ports.
- **`apps/auth` is a build-watcher, not a dev server.** Its `dev` script is `vite build --watch`; it is modeled
  with `AddJavaScriptApp` (no HTTP endpoint) so Aspire runs the watcher and the Playground serves the resulting
  `dist`. Only `apps/mgmt` (a true dev server) uses `AddViteApp`.
- **Per-app dependency auto-install is disabled; the workspace is installed once out-of-band.** Both JS resources
  use `WithPnpm(install: false)` and rely on `build/frontend-install.ps1` having installed the pnpm workspace.
  Aspire's default is to auto-install per resource, but two `pnpm install` processes running concurrently against
  the shared, symlinked `node_modules` of a pnpm **workspace** collide (`ERR_PNPM_EBUSY: resource busy or locked,
  rename ...`) and fail the losing installer, which in turn fails its dependent resource.
- **Never packed, excluded from publish.** The AppHost is `IsPackable=false` (so the provenance/PublicAPI/pack
  gates in [`Directory.Build.targets`](../../Directory.Build.targets) skip it) and contributes no shippable
  artifact. The `Aspire.AppHost.Sdk` MSBuild SDK is pinned in lockstep in [`global.json`](../../global.json)
  (`msbuild-sdks`) and the `Aspire.Hosting.*` packages in [`Directory.Packages.props`](../../Directory.Packages.props)
  ([ADR-0003](0003-central-package-management-and-lockstep-versioning.md)).
- **Backing services (SQL Server, Redis) are opt-in, modeled later as containers.** The default keeps the
  Playground's zero-setup SQLite/in-memory story so there is **no hard container-runtime prerequisite** just to run
  the stack.

`WithReference(api)` also exposes Aspire service discovery to the `mgmt` process; the browser SPA continues to
resolve the API from the host-published bootstrap document per [ADR-0064](0064-frontend-presentation-platform.md),
which points at the pinned `api` origin.

## Options considered

- **Keep the VS Code compound + `build/dev-ui.ps1` (status quo).** Zero new dependencies, but structurally unable
  to order startup — the race is inherent to a compound launch. Rejected.
- **A bespoke script that polls `/health` before starting the UIs.** Reinvents a slice of Aspire (health-gated
  ordering) with none of the dashboard, log/trace aggregation, service discovery, or future container modeling, and
  becomes per-app bespoke glue the moment a surface is added. Rejected.
- **Docker Compose.** Orders startup, but imposes a container-runtime prerequisite on every developer and containers
  the .NET host, which is heavier than the SQLite/in-memory inner loop the Playground deliberately offers. Deferred
  as a possible _publish_ target, not the local inner loop.
- **.NET Aspire sibling AppHost (chosen).** `WaitFor` + ASP.NET Core health-check integration gives deterministic,
  health-gated ordering; the dashboard is one place for ordered startup, logs, and traces; first-class JavaScript
  hosting (`Aspire.Hosting.JavaScript`: `AddViteApp`/`AddJavaScriptApp` + `WithPnpm`) models the pnpm workspace; and
  backing services can later be added as opt-in container resources. No workload or external CLI is required — the
  SDK and hosting packages restore from nuget.org and the AppHost launches via `dotnet run` / F5.

## Consequences

- **Easier:** one launch starts the whole stack in the right order; the dashboard shows each resource's health,
  logs, and traces; a new surface is one `AddViteApp`/`AddJavaScriptApp` line; backing SQL/Redis can be added as
  opt-in containers without touching the apps.
- **Harder / new prerequisites:** the AppHost restores the Aspire SDK + hosting packages (first restore is larger);
  Aspire discovers `node`/`pnpm` on `PATH`, which the AppHost handles by auto-prepending the newest NVS-managed
  Node 22 (`%LOCALAPPDATA%\nvs\node\22*\x64`, where Corepack also places the `pnpm` shim) to its own `PATH` — so
  `nvs add 22` suffices and no global `nvs link` is needed (a system/global Node already on `PATH` is used as-is);
  and because per-app auto-install is disabled, the pnpm workspace **must** be installed once from the root via
  `build/frontend-install.ps1` (which also runs `corepack enable`) before the first launch and again whenever
  dependencies change.
- **Retired:** the VS Code `API + UI (F5)` compound and the `UI: Frontend dev` node-terminal profile are replaced by
  the single `Aspire: Full stack (F5)` launch; `build/dev-ui.ps1` is relegated to `build/frontend-install.ps1` (a
  one-time workspace bootstrap) now that Aspire owns `pnpm dev`.
- **You chose wrong smells:** if the API is proxied through Aspire in a way that rewrites the request host, the
  issuer / OAuth cookies / antiforgery binding would drift from `localhost:5001` — the un-proxied, pinned endpoints
  guard against that. If backing containers ever become mandatory for a basic run, the zero-setup promise is broken;
  keep them opt-in.

## References

- [ADR-0064](0064-frontend-presentation-platform.md) — frontend presentation platform & runtime discovery.
- [ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md) — OP-hosted interactive login surface
  (`apps/auth` served from `dist`); this ADR supersedes that one's dev-orchestration half.
- [ADR-0003](0003-central-package-management-and-lockstep-versioning.md) — central package management & lockstep
  versioning (SDK in `global.json`, packages in `Directory.Packages.props`).
- Aspire JavaScript hosting (`Aspire.Hosting.JavaScript`: `AddViteApp`/`AddJavaScriptApp`, `WithPnpm`) and resource
  health checks (`WithHttpHealthCheck` + `WaitFor`).
- [`BACKLOG.md`](../../BACKLOG.md) `U11` (the decision this ADR records).
