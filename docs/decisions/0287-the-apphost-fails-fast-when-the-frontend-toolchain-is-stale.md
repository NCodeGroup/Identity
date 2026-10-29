# 287. The AppHost fails fast when the frontend toolchain is stale

- **Status:** Accepted (amends [ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md):
  the AppHost no longer skips a missing pinned Node silently)
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

Issue #401 reported what happens after a toolchain change. The frontend moved from Node 22 to Node 26, pnpm 9.12.0
became pnpm 10, and the workspace gained the packages `@ncode/vite-config` and `@ncode/test-support`. A developer who
pulled the change and started the Aspire AppHost without re-running `./build/frontend-install.ps1` got no error at
startup. The AppHost started, and the four frontend resources (`auth`, `mgmt`, `playground` and `conformance`) exited
about 300 ms later with module-resolution errors that were visible only in their console logs.

[ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md) made the AppHost put the
newest NVS install of the pinned Node major on `PATH` and skip that step silently when there was none, and
[ADR-0071](0071-aspire-local-dev-orchestrator.md) says the AppHost never installs. Nothing checked whether the Node on
`PATH` or the installed workspace still matched the repository.

## Decision

**The AppHost checks the frontend toolchain before it declares any resource, and fails at startup with one message that
lists every problem and names `./build/frontend-install.ps1`.**

1. **Where it lives.** The check is in the `NCode.Identity.OpenId.LocalDev` project under `Frontend/`, as `internal`
   types (`NvsNode`, `WorkspaceInstall` and `FrontendToolchainGuard`) behind one call in `Program.cs`, which stays
   composition-only. The former `EnsurePinnedNodeOnPath` local function moved into `NvsNode.FindDirectory` and
   `FrontendToolchainGuard`. The NVS lookup is unchanged: Windows-only, the newest numeric version of the pinned major,
   `x64`, prepended to `PATH` without duplicates.
2. **Node check.** After the NVS install is on `PATH`, the guard runs `node --version` (10 s timeout) with that `PATH`
   and requires its major to equal the major pinned in `src/frontend/.node-version`. This covers both "NVS has no
   install and `node` on `PATH` is another major" (the reported case: Node 24 against a pin of 26) and "no `node` at
   all", on every OS. The test is equality with the pinned major rather than the `engines` range, because ADR-0152 pins
   one major.
3. **Workspace check.** The guard reads files that pnpm itself writes:
   - `node_modules/.modules.yaml` exists and its `packageManager` equals `pnpm@<version>` from `src/frontend/package.json`.
     The file is JSON despite its extension. This catches a pnpm pin change (the reported pnpm 9.12.0 install).
   - `node_modules/.pnpm/lock.yaml`, pnpm's copy of the lockfile made at install time, equals `pnpm-lock.yaml` after
     CRLF is normalized to LF. This catches new workspace packages and any dependency change since the last install.

   A missing `pnpm-lock.yaml` is not a problem. A missing `node_modules` is.
4. **One report.** Every problem is collected, and the guard throws one `InvalidOperationException` that lists them all
   and names `./build/frontend-install.ps1`, so one run of the installer repairs everything. An unhandled exception in
   top-level `Program.cs` fails the AppHost before any resource starts, and a debugger shows the message.
5. **No opt-out.** The issue asks for fail-fast and the repair is one command, so there is no switch. pnpm's own presence
   on `PATH` is not checked; the issue names two conditions.

## Options considered

- **Run `pnpm install --frozen-lockfile --offline` or `pnpm ls` as the check.** Rejected: it spawns pnpm, is slow, and
  needs pnpm on `PATH`.
- **An Aspire startup command or a health check per resource.** Rejected: the stack would still start, and the
  resources would still exit one by one.
- **Installing from the AppHost.** Rejected: it collides on the shared workspace, and
  [ADR-0071](0071-aspire-local-dev-orchestrator.md) and ADR-0152 say the AppHost never installs.
- **Comparing the `engines.node` range instead of the pinned major.** Rejected: ADR-0152 pins one major, and the range
  is only its floor.

## Consequences

- A developer re-runs `./build/frontend-install.ps1` after pulling a toolchain change, and the AppHost says so at
  startup instead of leaving four resources to fail in their logs.
- The check costs a few file reads and one `node --version`; it needs no pnpm process.
- The AppHost never installs.
- A false positive is repaired by the same command.
- The ambient seams (the NVS root, `PATH` and the `node --version` call) are `internal virtual` passthroughs excluded
  from coverage, and the tests in `tests/NCode.Identity.OpenId.LocalDev.Tests` override them. That project references
  the AppHost project and sees its internals through `InternalsVisibleTo`, so `build/affected-tests.ps1` selects it when
  the AppHost changes.
- There is no behavior change on non-Windows beyond the new Node check.

## References

- [ADR-0071](0071-aspire-local-dev-orchestrator.md) — the Aspire local development orchestrator
- [ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md) — the pinned Node and pnpm
- [`build/frontend-install.ps1`](../../build/frontend-install.ps1)
- [`src/frontend/.node-version`](../../src/frontend/.node-version)
- [`src/NCode.Identity.OpenId.LocalDev/Program.cs`](../../src/NCode.Identity.OpenId.LocalDev/Program.cs)
- Issue #401
