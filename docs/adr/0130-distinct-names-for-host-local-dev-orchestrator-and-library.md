# 130. The deployable host, the local-dev orchestrator, and the shipped library have distinct names

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

Three projects in this repository sounded like the same thing and were not:

| Project (before)                             | What it actually is                        | Ships? |
| -------------------------------------------- | ------------------------------------------ | ------ |
| `NCode.Identity.Server`                      | library — the `AddIdentityServer()` package | yes    |
| `NCode.Identity.OpenId.Playground`           | the runnable OpenID Provider process        | no     |
| `NCode.Identity.OpenId.Playground.AppHost`   | the .NET Aspire local-dev orchestrator      | no     |

Two separate problems overlapped here.

**"Playground" named the wrong thing.** The name says "toy, for experimenting". That was honest while the project was
only a local test bed, but this is the process that is actually going to be deployed and face the internet. A name
that tells a reader not to take a deployable OpenID Provider seriously is a hazard: it invites exactly the "it's only
the playground" reasoning that leaves ephemeral signing keys, wildcard `AllowedHosts`, and dev seeding switched on in
something publicly reachable.

**`Server` / `Host` / `AppHost` read as synonyms.** Nothing in the three names distinguishes a published NuGet
package from a deployable process from a developer-only tool. `AppHost` is the worst offender: it is Aspire's own
jargon, and it sounds like *the* host of the application, when in this repository Aspire is **local-development
only** ([ADR-0071](0071-aspire-local-dev-orchestrator.md)) and real infrastructure deployment is OpenTofu. The
dev-only tool carried the most authoritative-sounding name while the actual deployable did not.

## Decision

| Project (after)                               | Role                                            | Ships? |
| --------------------------------------------- | ----------------------------------------------- | ------ |
| `NCode.Identity.Server`                       | library — the `AddIdentityServer()` package      | yes    |
| `NCode.Identity.OpenId.Host`                  | the deployable OpenID Provider                   | no     |
| `NCode.Identity.OpenId.Host.Database`         | EF migrations owned by that deployable           | no     |
| `NCode.Identity.OpenId.Host.MigrationService` | one-shot migration worker for that deployable    | no     |
| `NCode.Identity.OpenId.LocalDev`              | the Aspire local-dev orchestrator                | no     |

1. **`Host` is the thing you deploy.** The `Host.` prefix groups the deployable with the two projects that belong to
   it. `.Database` keeps the prefix because migrations are host-owned
   ([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md)); they travel with the deployable.

2. **The orchestrator is named for its purpose, not its technology.** `LocalDev` states the one fact a reader most
   needs: this is never deployed. `AppHost` was rejected for sounding central, and `Aspire` for naming the current
   implementation — if the orchestrator is ever rebuilt on something else, `LocalDev` is still true. This is safe
   because Aspire discovers the orchestrator through `<IsAspireHost>true</IsAspireHost>` and `Aspire.AppHost.Sdk`,
   **not** the project name. "AppHost" survives in prose and in package identifiers (`Aspire.Hosting.AppHost`) where
   it is Aspire's vocabulary for the role rather than a name for our project.

3. **`NCode.Identity.Server` is unchanged.** It is the only one of the three that ships, and `AddIdentityServer()` is
   its public entry point. The renamed projects are all `IsPackable=false`; the split between "library you reference"
   and "process you run" is now visible in the names.

4. **The frontend `apps/playground` keeps its name.** It genuinely is a playground — an interactive flow explorer
   ([ADR-0082](0082-playground-and-conformance-surfaces.md)) — along with the `playground-spa` client and the
   `playground` bootstrap surface. Only the .NET projects were renamed.

## Consequences

Every project path, namespace, and Aspire `Projects.*` reference moved in one cut. `PlaygroundApiMarker`,
`PlaygroundDatabase`, and `PlaygroundApplicationFactory` became `HostApiMarker`, `HostDatabase`, and
`HostApplicationFactory`.

Prose had to be read rather than substituted, because "Playground" meant two different things in the same sentences
in places — the .NET host in most, the flow-explorer SPA in others. A blind find-and-replace would have renamed the
SPA too.

The name no longer excuses unsafe defaults, which makes the remaining production-readiness gaps harder to ignore:
refusing to boot in Production on ephemeral signing keys or `AllowedHosts: "*"` is tracked as follow-up work, and so
is collapsing the development-only registrations behind a single self-gating call.
