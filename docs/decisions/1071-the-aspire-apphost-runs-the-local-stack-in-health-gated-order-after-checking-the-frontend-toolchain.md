# 1071. The Aspire AppHost runs the local stack in health-gated order after checking the frontend toolchain

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

Running the family locally is a multi-process exercise: the reference OpenID Provider host, a second provider for the
conformance suite, the management console's BFF host, and the frontend surfaces, which are a pnpm and Turborepo
workspace with a toolchain of its own, deliberately outside the hermetic [`dod.ps1`](../../build/dod.ps1) inner loop
([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)).

A VS Code compound launch starts its members concurrently with no dependency ordering, so the browser surfaces boot
before the API listens: the first runtime-discovery and management calls race the API and fail in the browser, and the
login surface answers 404 until its first build lands. A compound launch also gives no single place for ordered startup,
aggregated logs or traces.

Several invariants of the local setup assume fixed ports: the developer seed registers the surfaces' HTTPS callback
redirect URIs, the developer CORS origins allowlist their origins, the issuers are pinned, and the bootstrap document
points each surface at its provider. Any orchestration must preserve those ports and not allocate them dynamically.

The orchestrator also meets a toolchain that changes under a developer. After a pull that changes the pinned Node major,
the pinned pnpm or the workspace packages, a developer who does not re-run the installer gets no error at startup: the
AppHost starts, and the four frontend resources (`auth`, `mgmt`, `playground` and `conformance`) exit within a fraction
of a second with module-resolution errors visible only in their console logs (issue #401). Nothing checks whether the
Node on `PATH` or the installed workspace still matches the repository.

## Decision

**`NCode.Identity.OpenId.LocalDev` is an Aspire AppHost that models the whole local stack as one resource graph, starts
each resource only after the resources it needs are healthy, and checks the frontend toolchain before it declares any
resource.**

### The AppHost

`NCode.Identity.OpenId.LocalDev` lives under `hosts/`, a sibling of the OP host and never nested in it: the host stays a
single reference OP process, and the AppHost is a separate orchestration host (one `DistributedApplication`) that
references it. The `Aspire.AppHost.Sdk` MSBuild SDK is pinned in [`global.json`](../../global.json) `msbuild-sdks` and
the `Aspire.Hosting.*` packages in [`Directory.Packages.props`](../../Directory.Packages.props), in lockstep
([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)). The
`Aspire: Full stack (F5)` launch is the one entry that builds, runs and debugs the stack. The AppHost needs a container
runtime (Docker or Podman) for Postgres and Valkey.

### The resource graph

- `postgres` with the `openiddb` database ([ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md)),
  `valkey` and the `migrations` worker.
- `api`, the reference OP, on `https://localhost:5001` and `http://localhost:5000`.
- `conformanceapi`, a second OP under the `ConformanceAspire` launch profile, on `https://localhost:5003` and
  `http://localhost:5002`.
- `auth` and `mgmt`, the build-watchers of `apps/auth` and `apps/mgmt`.
- `console`, the management console's BFF host, on `https://localhost:5005`
  ([ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)).
- `playground` (port 5174) and `conformance` (port 5175), the Vite dev servers.

### Ordering waits on health

Ordering waits on health, not on process start. `api`, `conformanceapi` and `console` declare
`WithHttpHealthCheck("/health/ready")`, the host's readiness endpoint, which runs every check tagged
`HealthCheckTags.Readiness`. The OPs `WaitFor(valkey)` and `WaitForCompletion(migrations)`; `console` and `playground`
`WaitFor(api)`; `conformance` `WaitFor(conformanceapi)`. Aspire holds a dependent resource until the health check
returns `200`, which removes the surface-before-API race.

### Fixed, un-proxied ports

Ports are fixed. `api`, `conformanceapi` and `console` take theirs from their launch profiles, and the `playground`
and `conformance` Vite servers pin 5174 and 5175 un-proxied (`IsProxied = false`) so the OAuth redirect callback
resolves. The seeded redirect URIs, the developer CORS origins, the pinned issuers and the bootstrap document assume
these ports, and a proxy that rewrites the request host would drift the issuer, the OAuth cookies and the antiforgery
binding away from the pinned origin. Aspire's dynamic port allocation is rejected for that reason.

### HTTPS everywhere

Every browser surface is served over HTTPS with the ASP.NET Core development certificate. The Vite servers use
`WithHttpsDeveloperCertificate`: Aspire runs Vite under a wrapper config that sets `server.https` from the certificate
and switches the endpoint's scheme to `https`, so the browser trusts one certificate across the stack
(`dotnet dev-certs https --trust`). Outside Aspire the `defineStandaloneSurfaceConfig` preset serves the same
certificate, exported as PEM by `build/frontend-install.ps1`, and defers to `TLS_CONFIG_PFX` when Aspire sets it. The
seeded redirect URIs and the developer CORS origins register `https://` only.

### Build-watchers and dev servers

`apps/auth` and `apps/mgmt` are `vite build --watch` build-watchers, modeled with `AddJavaScriptApp` and no endpoint.
The OP serves `auth`'s `dist` under `/account/*`, and the console host serves `mgmt`'s. Only `playground` and
`conformance` are `AddViteApp` servers. Because no OP host serves their bundles, each proxies the well-known
`/config/bootstrap.json` to its OP through the service-discovery variable that `WithReference` injects, so local
development consumes the real runtime bootstrap endpoint and cannot drift from production
([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)).

### One serial installer

Every JS resource uses `WithPnpm(install: false)`. Aspire's default is to install per resource, but concurrent
`pnpm install` runs against the shared, symlinked `node_modules` of a pnpm workspace collide (`ERR_PNPM_EBUSY`) and fail
the losing installer and its dependent resource. `build/frontend-install.ps1` is therefore the single, serial installer
and repair point: after its fast `--frozen-lockfile` install it probes every workspace package's directly declared
links and force-reinstalls only when one is missing or broken. The AppHost never installs, and a per-app
`pnpm install` is never the repair.

### The toolchain guard

Before declaring any resource, the composition-only `Program.cs` calls `FrontendToolchainGuard.EnsureReady()`. The
guard and its internal helpers `NvsNode`, `WorkspaceInstall` and `FrontendPins` live under `Frontend/`.

1. **Node.** On Windows the guard prepends the `x64` directory of the newest numeric NVS install of the major in
   `frontend/.node-version` to `PATH`, without duplicates. On every OS it then runs `node --version` (10 s timeout) with
   that `PATH` and requires its major to equal the pinned major. This covers both a `node` of another major and no `node`
   at all. The test is equality with the pinned major, not the `engines.node` range, because the repository pins one
   major ([ADR-1152](1152-the-frontend-pins-node-and-pnpm-once-gates-dependencies-by-release-age-and-tracks-supported-upstreams.md)).
2. **Workspace.** The guard reads files that pnpm writes. `node_modules/.modules.yaml` (JSON despite its extension) must
   name `pnpm@<version>` from `frontend/package.json`'s `packageManager`, which catches a pnpm pin change.
   `node_modules/.pnpm/lock.yaml`, pnpm's copy of the lockfile made at install time, must equal `pnpm-lock.yaml` after
   CRLF is normalized to LF, which catches new workspace packages and any dependency change. A missing `node_modules` is
   a problem; a missing lockfile is not.
3. **One report.** The guard collects every problem and throws one `InvalidOperationException` that lists them all and
   names `./build/frontend-install.ps1`, so one run of the installer repairs everything. The unhandled exception in
   top-level `Program.cs` fails the AppHost before any resource starts, and a debugger shows the message.
4. **No opt-out.** Fail-fast costs one command to repair, so there is no switch. The guard does not check pnpm on
   `PATH`.

The guard's ambient seams (the NVS root, `PATH` and the `node --version` call) are `internal virtual` passthroughs
excluded from coverage, and `tests/NCode.Identity.OpenId.LocalDev.Tests` overrides them. That project references the
AppHost and sees its internals through `InternalsVisibleTo`, so `build/affected-tests.ps1` selects it when the AppHost
changes.

## Options considered

- **A VS Code compound launch with a script that runs `pnpm dev`.** It needs no new dependency, but it cannot order
  startup: the race is inherent to a compound launch. Rejected.
- **A bespoke script that polls `/health/ready` before starting the surfaces.** It reinvents a slice of Aspire with none
  of the dashboard, log and trace aggregation or service discovery, and becomes per-app glue whenever a surface is
  added. Rejected.
- **Docker Compose.** It orders startup, but it containerizes the .NET hosts, which is heavier than the inner loop and
  not suited to attaching a debugger. Rejected.
- **Aspire's dynamic ports.** The seeded redirect URIs, the developer CORS origins, the pinned issuers and the bootstrap
  document assume fixed ports. Rejected.
- **`pnpm install --offline` or `pnpm ls` as the toolchain check.** It spawns pnpm, is slow, and needs pnpm on `PATH`.
  Rejected.
- **A per-resource health check or startup command for the toolchain.** The stack would still start, and the resources
  would still exit one by one. Rejected.
- **Installing from the AppHost.** Concurrent installs collide on the shared workspace. Rejected.
- **Comparing the `engines.node` range instead of the pinned major.** The repository pins one major, and the range is
  only its floor. Rejected.
- **A .NET Aspire AppHost that gates ordering on health and checks the toolchain before it starts anything (chosen).**
  `WaitFor` with the ASP.NET Core health-check integration orders startup deterministically, the dashboard is one place
  for health, logs and traces, and first-class JavaScript hosting (`Aspire.Hosting.JavaScript`: `AddViteApp`,
  `AddJavaScriptApp`, `WithPnpm`) models the pnpm workspace. No workload or external CLI is required: the SDK and
  hosting packages restore from nuget.org and the AppHost launches through `dotnet run` or F5.

## Consequences

- One launch starts the whole stack in the right order, and the dashboard shows each resource's health, logs and
  traces. A new surface is one `AddViteApp` or `AddJavaScriptApp` line.
- A developer re-runs `./build/frontend-install.ps1` after pulling a toolchain change, and the AppHost says so at
  startup instead of leaving four resources to fail in their logs. The check costs a few file reads and one
  `node --version`, needs no pnpm process, and a false positive is repaired by the same command.
- The AppHost restores the Aspire SDK and hosting packages, needs a container runtime, and needs the pnpm workspace
  installed once from the root with `build/frontend-install.ps1` before the first launch and again whenever
  dependencies change.
- You chose wrong smells: if the API is proxied through Aspire in a way that rewrites the request host, the issuer,
  OAuth cookies and antiforgery binding drift from `localhost:5001`, which the pinned, un-proxied endpoints guard
  against. If a per-app `pnpm install` appears in the AppHost, the `ERR_PNPM_EBUSY` collision returns.
- The `hosts/` folder's `Directory.Build.props` turns packing off, so the AppHost contributes no shippable artifact.

## References

- [`hosts/NCode.Identity.OpenId.LocalDev/Program.cs`](../../hosts/NCode.Identity.OpenId.LocalDev/Program.cs) and
  [`Frontend/`](../../hosts/NCode.Identity.OpenId.LocalDev/Frontend/)
- [`build/frontend-install.ps1`](../../build/frontend-install.ps1) and
  [`frontend/.node-version`](../../frontend/.node-version)
- [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md): the frontend presentation platform and runtime discovery.
- [ADR-1152](1152-the-frontend-pins-node-and-pnpm-once-gates-dependencies-by-release-age-and-tracks-supported-upstreams.md): the pinned Node and pnpm.
- [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md): the Postgres database and the migration worker.
- [ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md): the console BFF
  host.
- [ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md):
  lockstep versioning.
- Aspire JavaScript hosting and resource health checks (`WithHttpHealthCheck`, `WaitFor`).
- Issues [#143](https://github.com/NCodeGroup/identity/issues/143) and #401.
